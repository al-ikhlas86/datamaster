using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace DataMaster.Launcher;

// Auto-update lewat GitHub REST API. Kelas ini ADAPTASI dari pola Presensi
// (D:\Presensi\src\Presensi\Services\UpdateService.cs), disesuaikan utk: (1)
// satu target win-x64 self-contained saja (DataMaster tidak perlu dukungan
// net48 spt kiosk lama), (2) proses ANAK (DataMaster.Web) yang juga harus
// dimatikan sebelum file ditimpa - lihat ApplyAndRestart().
//
// TANPA token/Authorization header (2026-09-12, repo "datamaster-desktop"
// diubah jadi PUBLIC) - endpoint REST API GitHub (metadata rilis MAUPUN
// unduh asset lewat /releases/assets/{id}) bisa diakses SIAPA SAJA tanpa
// kredensial apa pun kalau repo-nya public, PERSIS pola yang sudah lama
// terbukti jalan di Presensi. Dulu, SEBELUM repo ini public, wajib pakai
// fine-grained PAT tertanam di kode (LauncherConfig.EmbeddedGithubToken,
// SUDAH DIHAPUS) krn endpoint API repo privat menolak permintaan anonim -
// sekarang kebutuhan itu hilang sama sekali, bukan dipindah ke tempat lain.
public class UpdateChecker
{
    // Diamati MainWindow utk tampilkan status "jangan tutup aplikasi" di splash -
    // pelajaran nyata dari Presensi: unduhan besar TANPA tanda visual apa pun
    // bikin user mengira "tidak terjadi apa-apa" lalu menutup app, unduhan hangus.
    public event Action<string?>? StatusChanged;

    // ProgressChanged (2026-09-24, diminta user - "3-4 menit kerasa 10-15
    // menit") - SEBELUMNYA unduhan ~150MB ditelan bulat-bulat lewat
    // ReadAsByteArrayAsync() TANPA tanda kemajuan sama sekali - splash cuma
    // nampilin teks statis "Memperbarui..." konstan selama 2-4,5 menit penuh
    // (diverifikasi LANGSUNG dari log timestamp: proses SEBENARNYA cuma
    // 2m41d-4m37d total, BUKAN 10-15 menit spt dikira - murni persepsi krn
    // tidak ada indikator kemajuan sama sekali). null = indeterminate (fase
    // cek versi, belum tahu ukuran), 0-100 = persentase unduhan sungguhan.
    public event Action<double?>? ProgressChanged;

    private const string AssetName = "DataMaster-win-x64.zip";
    private const string ApiLatestReleaseUrl = "https://api.github.com/repos/al-ikhlas86/datamaster-desktop/releases/latest";
    private const string ApiAssetUrlTemplate = "https://api.github.com/repos/al-ikhlas86/datamaster-desktop/releases/assets/{0}";

    // Kembalikan true kalau update DITERAPKAN (artinya Application.Current.Shutdown()
    // SUDAH dipanggil di dalam ApplyAndRestart - pemanggil WAJIB berhenti lanjut,
    // JANGAN membuka wizard/MainWindow lagi krn proses ini sudah dalam proses mati).
    public async Task<bool> CheckAndApplyAsync(ServerProcessManager server, CancellationToken ct)
    {
        try
        {
            Log("Mulai cek update...");
            var installed = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
            Log($"Versi terpasang: {installed}.");

            // Timeout DIPISAH per-request (bukan 1 batas global) - HttpClient.Timeout
            // dibiarkan TANPA batas di level klien, batasnya diterapkan PER PANGGILAN
            // lewat CancellationTokenSource: 15 detik utk cek metadata (SEHARUSNYA
            // instan - kalau lambat sampai 15 detik pun, itu tanda koneksi genuinely
            // bermasalah, jangan bikin user nunggu lama di splash cuma buat cek versi),
            // 20 menit KHUSUS unduhan asset ~150MB di bawah (root cause NYATA
            // ditemukan 2026-09-10: 1 batas 30 detik utk KEDUANYA bikin unduhan
            // SELALU gagal di tengah jalan, dulu diam2 tertelan tanpa log).
            using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            // GitHub API MEWAJIBKAN User-Agent (request tanpa ini ditolak 403).
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DataMaster-AlIkhlas86-Updater");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            string releaseJson;
            using (var ctsMeta = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                ctsMeta.CancelAfter(TimeSpan.FromSeconds(15));
                releaseJson = await http.GetStringAsync(ApiLatestReleaseUrl, ctsMeta.Token);
            }
            using var doc = JsonDocument.Parse(releaseJson);
            var tagName = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            Log($"Rilis terbaru di GitHub: {tagName}.");

            if (!Version.TryParse(NormalizeVersion(tagName.TrimStart('v', 'V')), out var remote))
            {
                Log($"GAGAL parse tag_name '{tagName}' sbg versi - dilewati.");
                return false;
            }
            if (remote <= installed)
            {
                Log($"Sudah versi terbaru ({installed} >= {remote}) - tidak ada yang diunduh.");
                return false; // sudah versi terbaru
            }

            long assetId = 0;
            long assetSize = 0;
            foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() == AssetName)
                {
                    assetId = asset.GetProperty("id").GetInt64();
                    assetSize = asset.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;
                    break;
                }
            }
            if (assetId == 0)
            {
                Log($"Rilis {tagName} ADA tapi asset '{AssetName}' TIDAK DITEMUKAN - dilewati.");
                return false; // rilis ada tapi belum ada asset yang cocok - dilewati
            }

            var sizeMb = assetSize > 0 ? $"{assetSize / 1024.0 / 1024.0:F0} MB" : "ukuran tidak diketahui";
            Log($"Update ditemukan: {installed} -> {remote} ({sizeMb}). Mulai unduh asset id={assetId}...");
            StatusChanged?.Invoke($"Memperbarui ke versi {remote} ({sizeMb}) - JANGAN TUTUP APLIKASI INI sampai selesai...");

            using var assetReq = new HttpRequestMessage(HttpMethod.Get, string.Format(ApiAssetUrlTemplate, assetId));
            // Accept ini WAJIB - tanpa ini GitHub API balikin metadata JSON asset,
            // BUKAN isi berkasnya.
            assetReq.Headers.Accept.ParseAdd("application/octet-stream");
            byte[] zipBytes;
            using (var ctsUnduh = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                ctsUnduh.CancelAfter(TimeSpan.FromMinutes(20));
                // HttpCompletionOption.ResponseHeadersRead (2026-09-24) - WAJIB
                // supaya body belum langsung ditelan bulat-bulat di sini, baru
                // di-stream manual di bawah sedikit demi sedikit (lihat catatan
                // ProgressChanged) - tanpa ini SendAsync sendiri sudah menunggu
                // SELURUH body sebelum baris berikutnya jalan, progress
                // per-chunk jadi percuma.
                using var assetResp = await http.SendAsync(assetReq, HttpCompletionOption.ResponseHeadersRead, ctsUnduh.Token);
                assetResp.EnsureSuccessStatusCode();
                var totalBytes = assetResp.Content.Headers.ContentLength ?? assetSize;
                await using var respStream = await assetResp.Content.ReadAsStreamAsync(ctsUnduh.Token);
                using var mem = new MemoryStream(totalBytes > 0 ? (int)totalBytes : 0);
                var buffer = new byte[81920];
                long totalRead = 0;
                var lastReport = DateTime.MinValue;
                int read;
                while ((read = await respStream.ReadAsync(buffer, ctsUnduh.Token)) > 0)
                {
                    await mem.WriteAsync(buffer.AsMemory(0, read), ctsUnduh.Token);
                    totalRead += read;
                    // Dibatasi max ~5x/detik (bukan tiap chunk 80KB, bisa
                    // ratusan kali/detik) - UI thread cukup, tidak perlu
                    // update lebih sering dari mata bisa lihat.
                    if (totalBytes > 0 && (DateTime.UtcNow - lastReport).TotalMilliseconds >= 200)
                    {
                        lastReport = DateTime.UtcNow;
                        var persen = totalRead * 100.0 / totalBytes;
                        ProgressChanged?.Invoke(persen);
                        StatusChanged?.Invoke($"Memperbarui ke versi {remote} ({totalRead / 1024.0 / 1024.0:F0}/{totalBytes / 1024.0 / 1024.0:F0} MB) - JANGAN TUTUP APLIKASI INI sampai selesai...");
                    }
                }
                ProgressChanged?.Invoke(100);
                zipBytes = mem.ToArray();
            }
            Log($"Unduhan selesai ({zipBytes.Length} bytes). Menerapkan pembaruan...");

            ProgressChanged?.Invoke(null); // balik indeterminate - tahap ekstrak/salin tidak py progress granular
            StatusChanged?.Invoke("Update selesai diunduh - aplikasi akan tertutup sebentar lalu terbuka lagi otomatis...");
            ApplyAndRestart(zipBytes, server);
            Log("ApplyAndRestart selesai dipanggil, Shutdown() diminta.");
            return true;
        }
        catch (Exception ex)
        {
            // Gagal cek/unduh (internet mati, token keliru/kedaluwarsa, GitHub
            // tidak terjangkau) TIDAK BOLEH mengganggu fungsi utama aplikasi -
            // dicoba lagi kesempatan berikutnya (start berikutnya). Banner
            // disembunyikan lagi - JANGAN dibiarkan nyangkut "sedang mengunduh".
            // DICATAT KE LOG (sebelumnya diam total - gap nyata yang bikin
            // kegagalan auto-update MUSTAHIL didiagnosis dari jarak jauh,
            // ditemukan 2026-09-10 saat update v1.1.0->v1.2.0 gagal tanpa jejak
            // sama sekali).
            Log($"GAGAL cek/terapkan update: {ex}");
            StatusChanged?.Invoke(null);
            ProgressChanged?.Invoke(null);
            return false;
        }
    }

    // Log berkas TERPISAH dari launcher_{tanggal}.log (App.xaml.cs, cuma exception
    // fatal) - update-checker dulu TIDAK PERNAH menulis apapun sama sekali walau
    // gagal, bikin kegagalan mustahil didiagnosis dari jarak jauh. File ini bisa
    // dibuka langsung staf/developer tanpa perlu debugger terpasang.
    private static void Log(string pesan)
    {
        try
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DataMaster", "logs");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, $"update_{DateTime.Now:yyyy-MM-dd}.log"), $"[{DateTime.Now:O}] {pesan}\n");
        }
        catch { /* logging tidak boleh ikut melempar error baru */ }
    }

    // "1.1.0" -> "1.1.0.0" - System.Version butuh >=2 bagian, dibuat selalu 4
    // bagian supaya perbandingan dgn versi assembly (SELALU 4 bagian) konsisten.
    private static string NormalizeVersion(string v)
    {
        var parts = v.Trim().Split('.');
        var padded = parts.Concat(Enumerable.Repeat("0", Math.Max(0, 4 - parts.Length))).Take(4);
        return string.Join(".", padded);
    }

    private static void ApplyAndRestart(byte[] zipBytes, ServerProcessManager server)
    {
        var installDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        var exePath = Path.Combine(installDir, "DataMaster.Launcher.exe");

        // Ekstrak ke folder SEMENTARA dulu (bukan langsung ke installDir) - aman
        // dilakukan SAAT APP MASIH JALAN krn tidak menyentuh file yang sedang
        // dikunci sama sekali. Baru dipindah oleh helper cmd.exe SETELAH app
        // (dan proses anak DataMaster.Web) benar2 tertutup di bawah.
        var stagingDir = Path.Combine(Path.GetTempPath(), "datamaster-update-" + Guid.NewGuid().ToString("N"));
        var zipPath = stagingDir + ".zip";
        Directory.CreateDirectory(stagingDir);
        File.WriteAllBytes(zipPath, zipBytes);
        ZipFile.ExtractToDirectory(zipPath, stagingDir);
        File.Delete(zipPath);

        // Matikan proses ANAK (DataMaster.Web) SEKARANG - berkasnya (di bawah
        // installDir\web\) ikut ditimpa xcopy nanti, kuncinya harus lepas dulu.
        // BEDA dari Presensi (aplikasi tunggal, tidak punya proses anak).
        server.StopIntentionally();
        // BUG NYATA ditemukan 2026-09-25 (PC TU SD - user lapor halaman
        // Setting tidak pernah menampilkan versi walau log update sudah
        // bilang sukses): StopIntentionally() di atas SENGAJA tidak
        // menyentuh Windows Service (lihat catatan panjang di situ) - kalau
        // TIDAK dimatikan EKSPLISIT di sini, Windows tetap mengizinkan xcopy
        // di bawah menimpa exe yang sedang berjalan TANPA error kelihatan,
        // tapi proses SERVICE LAMA (mode server) terus hidup memakai kode
        // LAMA di memori - file di disk sudah baru tapi kode yg BENAR2
        // dipakai tidak pernah ikut ter-update sampai service di-restart
        // manual/PC reboot. WindowsServiceHelper.EnsureStarted() (dipanggil
        // ulang saat Launcher relaunch di bawah) cuma menyalakan service yg
        // Stopped - TIDAK PERNAH me-restart yang masih Running - jadi harus
        // benar2 dimatikan DI SINI dulu, bukan mengandalkan langkah setelahnya.
        if (WindowsServiceHelper.IsInstalled()) WindowsServiceHelper.StopDanTungguUntukUpdate();

        var pid = Process.GetCurrentProcess().Id;
        var scriptPath = Path.Combine(Path.GetTempPath(), "datamaster-update.bat");
        // Loop tasklist menunggu PID Launcher benar2 keluar - pengganti pola
        // "update.lock dgn stale-timeout" PHP (§12) yang LEBIH SEDERHANA & tanpa
        // ambiguitas basi/tidak sama sekali: kalau PID sudah tidak ada, memang
        // sudah tidak ada, tidak perlu heuristik "berapa lama dianggap macet".
        var script =
            "@echo off\r\n" +
            ":wait\r\n" +
            $"tasklist /FI \"PID eq {pid}\" 2>NUL | find \"{pid}\" >NUL\r\n" +
            "if not errorlevel 1 (\r\n" +
            "    ping 127.0.0.1 -n 2 >NUL\r\n" +
            "    goto wait\r\n" +
            ")\r\n" +
            $"xcopy \"{stagingDir}\\*\" \"{installDir}\\\" /Y /E /I >NUL\r\n" +
            $"rmdir /S /Q \"{stagingDir}\"\r\n" +
            $"start \"\" \"{exePath}\"\r\n" +
            "del \"%~f0\"\r\n";
        File.WriteAllText(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        });

        // Tutup app SEKARANG - helper di atas sudah menunggu PID ini keluar
        // (via tasklist) sebelum menimpa file.
        Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
    }
}
