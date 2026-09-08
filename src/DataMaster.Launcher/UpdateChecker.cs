using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace DataMaster.Launcher;

// Auto-update lewat GitHub REST API (BUKAN URL publik "releases/latest/download/
// ..." - repo "datamaster-desktop" PRIVAT, dan URL semacam itu TERBUKTI SELALU
// 404 tanpa kredensial utk repo privat - dibuktikan langsung proyek saudara
// Presensi 2026-09-01, bukan asumsi. Lihat D:\Presensi\src\Presensi\Services\
// UpdateService.cs - kelas ini adalah ADAPTASI LANGSUNG dari pola itu yang
// sudah terbukti jalan di lapangan, disesuaikan utk: (1) satu target win-x64
// self-contained saja (DataMaster tidak perlu dukungan net48 spt kiosk lama),
// (2) proses ANAK (DataMaster.Web) yang juga harus dimatikan sebelum file
// ditimpa - lihat ApplyAndRestart().
//
// Token GitHub disimpan di LauncherConfig (appsettings.json sebelah .exe),
// BUKAN ditanam di kode - fine-grained PAT scope "Contents: Read-only" KHUSUS
// repo ini. Kosong = cek update dilewati diam-diam, aplikasi tetap jalan normal.
public class UpdateChecker
{
    // Diamati MainWindow utk tampilkan status "jangan tutup aplikasi" di splash -
    // pelajaran nyata dari Presensi: unduhan besar TANPA tanda visual apa pun
    // bikin user mengira "tidak terjadi apa-apa" lalu menutup app, unduhan hangus.
    public event Action<string?>? StatusChanged;

    private const string AssetName = "DataMaster-win-x64.zip";
    private const string ApiLatestReleaseUrl = "https://api.github.com/repos/al-ikhlas86/datamaster-desktop/releases/latest";
    private const string ApiAssetUrlTemplate = "https://api.github.com/repos/al-ikhlas86/datamaster-desktop/releases/assets/{0}";

    public async Task CheckAndApplyAsync(ServerProcessManager server, CancellationToken ct)
    {
        try
        {
            var config = LauncherConfig.Load();
            var token = config.GithubToken;
            if (string.IsNullOrWhiteSpace(token)) return; // belum dikonfigurasi - dilewati diam2, bukan error

            var installed = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            // GitHub API MEWAJIBKAN User-Agent (request tanpa ini ditolak 403).
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DataMaster-AlIkhlas86-Updater");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var releaseJson = await http.GetStringAsync(ApiLatestReleaseUrl, ct);
            using var doc = JsonDocument.Parse(releaseJson);
            var tagName = doc.RootElement.GetProperty("tag_name").GetString() ?? "";

            if (!Version.TryParse(NormalizeVersion(tagName.TrimStart('v', 'V')), out var remote)) return;
            if (remote <= installed) return; // sudah versi terbaru

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
            if (assetId == 0) return; // rilis ada tapi belum ada asset yang cocok - dilewati

            var sizeMb = assetSize > 0 ? $"{assetSize / 1024.0 / 1024.0:F0} MB" : "ukuran tidak diketahui";
            StatusChanged?.Invoke($"Memperbarui ke versi {remote} ({sizeMb}) - JANGAN TUTUP APLIKASI INI sampai selesai...");

            using var assetReq = new HttpRequestMessage(HttpMethod.Get, string.Format(ApiAssetUrlTemplate, assetId));
            // Accept ini WAJIB - tanpa ini GitHub API balikin metadata JSON asset,
            // BUKAN isi berkasnya.
            assetReq.Headers.Accept.ParseAdd("application/octet-stream");
            using var assetResp = await http.SendAsync(assetReq, ct);
            assetResp.EnsureSuccessStatusCode();
            var zipBytes = await assetResp.Content.ReadAsByteArrayAsync(ct);

            StatusChanged?.Invoke("Update selesai diunduh - aplikasi akan tertutup sebentar lalu terbuka lagi otomatis...");
            ApplyAndRestart(zipBytes, server);
        }
        catch
        {
            // Gagal cek/unduh (internet mati, token keliru/kedaluwarsa, GitHub
            // tidak terjangkau) TIDAK BOLEH mengganggu fungsi utama aplikasi -
            // dicoba lagi kesempatan berikutnya (start berikutnya). Banner
            // disembunyikan lagi - JANGAN dibiarkan nyangkut "sedang mengunduh".
            StatusChanged?.Invoke(null);
        }
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
