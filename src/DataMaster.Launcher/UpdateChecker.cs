using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;

namespace DataMaster.Launcher;

// Auto-update lewat GitHub REST API (repo publik, tanpa token).
//
// DIRANCANG ULANG (2026-11-27, permintaan pemilik: "pisahkan proses unduh dan menimpa; selama unduh sistem masih bisa dipakai; setelah selesai muncul
// popup restart"). Sebelumnya satu proses: layar pemuatan penuh selama mengunduh ~121 MB (di jaringan lambat 5 menit tak bisa dipakai), seluruh berkas
// ditahan di memori, tidak bisa dilanjutkan bila putus, dan tidak diperiksa keutuhannya. Sekarang tiga tahap terpisah:
//   1. UNDUH (latar belakang, aplikasi tetap bisa dipakai): ke berkas .part di %LocalAppData%\DataMaster\update, bisa DILANJUTKAN (Range) bila putus,
//      deteksi macet 60 dtk, ulang otomatis sampai 4x; lalu cek ukuran + SHA-256 (bila GitHub memberi digest) + isi zip.
//   2. SIAPKAN (latar belakang): ekstrak ke folder staging; baru dianggap "siap" setelah penanda siap.json ditulis.
//   3. TERAPKAN (hanya setelah pengguna setuju lewat popup "Mulai ulang sekarang?", atau otomatis saat aplikasi dibuka berikutnya): matikan server,
//      helper menyalin dari staging (robocopy) dan membuka aplikasi baru. Karena sudah di-staging, henti-nya hanya beberapa detik.
public class UpdateChecker
{
    // Pesan status singkat selama mengunduh (null = selesai/hilangkan). Bukan layar penuh: MainWindow hanya menampilkannya sebagai banner kecil.
    public event Action<string?>? StatusChanged;

    // null = tidak ada unduhan berjalan, 0-100 = persentase unduhan.
    public event Action<double?>? ProgressChanged;

    private const string AssetName = "DataMaster-win-x64.zip";
    private const string ApiLatestReleaseUrl = "https://api.github.com/repos/al-ikhlas86/datamaster/releases/latest";
    private const string ApiAssetUrlTemplate = "https://api.github.com/repos/al-ikhlas86/datamaster/releases/assets/{0}";

    // Maksimal percobaan MENERAPKAN update yang sama (versi tujuan sama, versi terpasang tak berubah) dalam 24 jam sebelum berhenti mencoba otomatis.
    private const int MaksPercobaan = 2;
    private const int MaksUlangUnduh = 4;
    private static readonly TimeSpan BatasMacet = TimeSpan.FromSeconds(60);

    // Satu pemeriksaan/unduhan pada satu waktu di dalam proses.
    private static readonly SemaphoreSlim KunciPeriksa = new(1, 1);

    // true = sesi ini jangan memeriksa/menerapkan update (dibuka ulang helper setelah penyalinan gagal; jangan langsung mencoba lagi).
    public static bool LewatiSesiIni { get; set; }
    // Pesan untuk pengguna bila update otomatis ditahan karena gagal berulang.
    public static string? PesanTahan { get; private set; }

    internal sealed record StatusPercobaan(string Target, int Percobaan, DateTime TerakhirUtc);
    internal sealed record Siap(string Versi, string Staging);

    private static string DataLokal => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DataMaster");
    private static string AkarUpdate => Path.Combine(DataLokal, "update");
    private static string BerkasStatus => Path.Combine(DataLokal, "update_state.json");
    private static string BerkasSiap => Path.Combine(AkarUpdate, "siap.json");

    private static StatusPercobaan? BacaStatus()
    {
        try { return File.Exists(BerkasStatus) ? JsonSerializer.Deserialize<StatusPercobaan>(File.ReadAllText(BerkasStatus)) : null; }
        catch { return null; }
    }

    private static void TulisStatus(StatusPercobaan s)
    {
        try
        {
            Directory.CreateDirectory(DataLokal);
            File.WriteAllText(BerkasStatus, JsonSerializer.Serialize(s));
        }
        catch { /* gagal menyimpan status tidak boleh menghentikan update */ }
    }

    private static void HapusStatus()
    {
        try { if (File.Exists(BerkasStatus)) File.Delete(BerkasStatus); }
        catch { /* abaikan */ }
    }

    private static Siap? BacaSiap()
    {
        try
        {
            if (!File.Exists(BerkasSiap)) return null;
            var s = JsonSerializer.Deserialize<Siap>(File.ReadAllText(BerkasSiap));
            return s is not null && Directory.Exists(s.Staging) && File.Exists(Path.Combine(s.Staging, "web", "DataMaster.Web.exe")) ? s : null;
        }
        catch { return null; }
    }

    private static Version Terpasang() => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

    /// <summary>Versi pembaruan yang sudah diunduh lengkap dan siap diterapkan (lebih baru dari yang terpasang), atau null.</summary>
    public static string? VersiSiap()
    {
        var siap = BacaSiap();
        return siap is not null && Version.TryParse(NormalizeVersion(siap.Versi), out var v) && v > Terpasang() ? siap.Versi : null;
    }

    private static void BersihkanSisaUpdate()
    {
        try { if (Directory.Exists(AkarUpdate)) Directory.Delete(AkarUpdate, true); }
        catch { /* masih terkunci: dicoba lagi lain kali */ }
    }

    /// <summary>
    /// Tahap 1+2, dipanggil di LATAR BELAKANG saat aplikasi sudah berjalan. Mengembalikan versi yang siap diterapkan (baru selesai disiapkan, atau sudah
    /// siap sebelumnya), atau null bila tidak ada pembaruan / gagal (sebabnya dicatat di update_*.log; tidak pernah melempar).
    /// </summary>
    public async Task<string?> PeriksaDanSiapkanAsync(CancellationToken ct)
    {
        if (!await KunciPeriksa.WaitAsync(0, ct))
        {
            Log("Pemeriksaan update lain masih berjalan - dilewati.");
            return null;
        }
        try { return await PeriksaCoreAsync(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return null; }
        catch (Exception ex)
        {
            Log($"GAGAL cek/siapkan update: {ex}");
            return null;
        }
        finally
        {
            KunciPeriksa.Release();
            StatusChanged?.Invoke(null);
            ProgressChanged?.Invoke(null);
        }
    }

    private async Task<string?> PeriksaCoreAsync(CancellationToken ct)
    {
        if (LewatiSesiIni)
        {
            Log("Sesi ini melewati pemeriksaan update (penyalinan update sebelumnya gagal).");
            return null;
        }
        Log("Mulai cek update...");
        var installed = Terpasang();
        Log($"Versi terpasang: {installed}.");

        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("DataMaster-AlIkhlas86-Updater");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        string releaseJson;
        using (var ctsMeta = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            ctsMeta.CancelAfter(TimeSpan.FromSeconds(20));
            releaseJson = await http.GetStringAsync(ApiLatestReleaseUrl, ctsMeta.Token);
        }
        using var doc = JsonDocument.Parse(releaseJson);
        var tagName = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
        Log($"Rilis terbaru di GitHub: {tagName}.");

        if (!Version.TryParse(NormalizeVersion(tagName.TrimStart('v', 'V')), out var remote))
        {
            Log($"GAGAL parse tag_name '{tagName}' sbg versi - dilewati.");
            return null;
        }
        if (remote <= installed)
        {
            Log($"Sudah versi terbaru ({installed} >= {remote}) - tidak ada yang diunduh.");
            HapusStatus();         // update sebelumnya (kalau ada) sudah berhasil
            BersihkanSisaUpdate(); // unduhan/staging lama tidak dipakai lagi
            return null;
        }
        var tujuan = remote.ToString();

        // Sudah pernah diunduh lengkap dan disiapkan untuk versi ini: tidak perlu mengunduh lagi.
        var sudahSiap = BacaSiap();
        if (sudahSiap is not null && sudahSiap.Versi == tujuan)
        {
            Log($"Pembaruan {tujuan} sudah diunduh dan siap diterapkan.");
            return tujuan;
        }

        var status = BacaStatus();
        if (status is not null && status.Target == tujuan && status.Percobaan >= MaksPercobaan && (DateTime.UtcNow - status.TerakhirUtc) < TimeSpan.FromHours(24))
        {
            Log($"Update ke {tujuan} sudah dicoba {status.Percobaan}x dan gagal diterapkan - pembaruan otomatis ditahan 24 jam. Lihat update_helper.log.");
            PesanTahan = PesanDitahan(remote, installed, status.Percobaan);
            return null;
        }

        long assetId = 0, assetSize = 0;
        string? digest = null;
        foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != AssetName) continue;
            assetId = asset.GetProperty("id").GetInt64();
            assetSize = asset.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;
            if (asset.TryGetProperty("digest", out var dg) && dg.ValueKind == JsonValueKind.String) digest = dg.GetString();
            break;
        }
        if (assetId == 0)
        {
            Log($"Rilis {tagName} ADA tapi asset '{AssetName}' TIDAK DITEMUKAN - dilewati.");
            return null;
        }

        // Sisa unduhan/staging versi lain dibuang; .part versi yang sama dipertahankan untuk dilanjutkan.
        BersihkanVersiLain(tujuan);
        Directory.CreateDirectory(AkarUpdate);
        var partPath = Path.Combine(AkarUpdate, $"DataMaster-{tujuan}.zip.part");
        var zipPath = Path.Combine(AkarUpdate, $"DataMaster-{tujuan}.zip");
        var staging = Path.Combine(AkarUpdate, $"staging-{tujuan}");

        Log($"Update ditemukan: {installed} -> {remote} ({assetSize / 1024.0 / 1024.0:F0} MB). Mulai unduh di latar belakang, asset id={assetId}...");
        await UnduhDenganUlangAsync(http, assetId, assetSize, partPath, remote.ToString(3), ct);

        StatusChanged?.Invoke($"Memeriksa keutuhan pembaruan {remote.ToString(3)}...");
        ProgressChanged?.Invoke(null);
        await Task.Run(() => Verifikasi(partPath, assetSize, digest), ct);
        if (File.Exists(zipPath)) File.Delete(zipPath);
        File.Move(partPath, zipPath);

        StatusChanged?.Invoke($"Menyiapkan pembaruan {remote.ToString(3)}...");
        await Task.Run(() => Ekstrak(zipPath, staging), ct);
        File.Delete(zipPath);

        File.WriteAllText(BerkasSiap, JsonSerializer.Serialize(new Siap(tujuan, staging)));
        Log($"Pembaruan {tujuan} siap diterapkan (staging: {staging}).");
        return tujuan;
    }

    private static string PesanDitahan(Version remote, Version installed, int percobaan) =>
        $"Pembaruan otomatis ke versi {remote.ToString(3)} gagal diterapkan {percobaan} kali. Aplikasi tetap bisa dipakai dengan versi sekarang ({installed.ToString(3)}).\n\n" +
        "Tutup SEMUA jendela Data Master (cek Task Manager), lalu buka lagi untuk mencoba kembali besok, atau minta Admin IT memperbarui secara manual. Rincian: %LocalAppData%\\DataMaster\\logs\\update_helper.log";

    private static void BersihkanVersiLain(string versiTujuan)
    {
        try
        {
            if (!Directory.Exists(AkarUpdate)) return;
            foreach (var entri in Directory.EnumerateFileSystemEntries(AkarUpdate))
            {
                var nama = Path.GetFileName(entri);
                if (nama == "siap.json" || nama.Contains(versiTujuan, StringComparison.Ordinal)) continue;
                try { if (Directory.Exists(entri)) Directory.Delete(entri, true); else File.Delete(entri); } catch { /* terkunci: biarkan */ }
            }
        }
        catch { /* pembersihan tidak boleh menggagalkan update */ }
    }

    // ---- tahap 1: unduh (bisa dilanjutkan) ----

    private async Task UnduhDenganUlangAsync(HttpClient http, long assetId, long assetSize, string partPath, string versi, CancellationToken ct)
    {
        Exception? terakhir = null;
        for (var percobaan = 1; percobaan <= MaksUlangUnduh; percobaan++)
        {
            try { await UnduhSekaliAsync(http, assetId, assetSize, partPath, versi, ct); return; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                terakhir = ex;
                Log($"Unduhan terputus (percobaan {percobaan}/{MaksUlangUnduh}): {ex.Message}. Dilanjutkan dari byte yang sudah ada.");
                StatusChanged?.Invoke("Unduhan pembaruan terputus, mencoba lagi... (aplikasi tetap bisa dipakai)");
                await Task.Delay(TimeSpan.FromSeconds(3 * percobaan * percobaan), ct);
            }
        }
        throw new IOException("Unduhan pembaruan gagal berulang kali.", terakhir);
    }

    private async Task UnduhSekaliAsync(HttpClient http, long assetId, long assetSize, string partPath, string versi, CancellationToken ct)
    {
        long ada = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;
        if (assetSize > 0 && ada > assetSize) { File.Delete(partPath); ada = 0; }
        if (assetSize > 0 && ada == assetSize) return; // unduhan sebelumnya sudah lengkap

        using var req = new HttpRequestMessage(HttpMethod.Get, string.Format(ApiAssetUrlTemplate, assetId));
        req.Headers.Accept.ParseAdd("application/octet-stream");
        if (ada > 0) req.Headers.Range = new RangeHeaderValue(ada, null);
        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (resp.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            File.Delete(partPath); // berkas sebagian tidak cocok: mulai dari nol di percobaan berikut
            throw new IOException("Berkas sebagian tidak cocok dengan server, mulai ulang.");
        }
        resp.EnsureSuccessStatusCode();
        var lanjut = resp.StatusCode == HttpStatusCode.PartialContent;
        if (!lanjut) ada = 0; // server mengabaikan Range: seluruh berkas dikirim ulang
        var total = assetSize > 0 ? assetSize : ada + (resp.Content.Headers.ContentLength ?? 0);

        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = new FileStream(partPath, lanjut ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16, useAsync: true);
        var buffer = new byte[256 * 1024];
        var jumlah = ada;
        var lapor = DateTime.MinValue;
        while (true)
        {
            // Macet (tidak ada byte masuk 60 dtk) dianggap putus: ditangkap pemanggil lalu dilanjutkan dari byte yang sudah ada.
            using var macet = CancellationTokenSource.CreateLinkedTokenSource(ct);
            macet.CancelAfter(BatasMacet);
            var n = await src.ReadAsync(buffer, macet.Token);
            if (n == 0) break;
            await dst.WriteAsync(buffer.AsMemory(0, n), ct);
            jumlah += n;
            if (total > 0 && (DateTime.UtcNow - lapor).TotalMilliseconds >= 300)
            {
                lapor = DateTime.UtcNow;
                ProgressChanged?.Invoke(jumlah * 100.0 / total);
                StatusChanged?.Invoke($"Mengunduh pembaruan {versi}: {jumlah / 1024.0 / 1024.0:F0} dari {total / 1024.0 / 1024.0:F0} MB (aplikasi tetap bisa dipakai)");
            }
        }
        await dst.FlushAsync(ct);
        if (total > 0 && jumlah < total) throw new IOException($"Unduhan terpotong ({jumlah} dari {total} byte).");
    }

    private static void Verifikasi(string partPath, long assetSize, string? digest)
    {
        var ukuran = new FileInfo(partPath).Length;
        if (assetSize > 0 && ukuran != assetSize)
        {
            File.Delete(partPath);
            throw new IOException($"Ukuran unduhan tidak sesuai ({ukuran} dari {assetSize} byte); dibuang, akan diunduh ulang.");
        }
        if (digest is not null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            using var fs = File.OpenRead(partPath);
            var hash = Convert.ToHexString(SHA256.HashData(fs));
            if (!hash.Equals(digest["sha256:".Length..], StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(partPath);
                throw new IOException("SHA-256 unduhan tidak cocok dengan rilis; dibuang, akan diunduh ulang.");
            }
            Log("SHA-256 unduhan cocok.");
        }
    }

    private static void Ekstrak(string zipPath, string staging)
    {
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            var nama = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!nama.Contains("DataMaster.Launcher.exe") || !nama.Contains("web/DataMaster.Web.exe"))
            {
                zip.Dispose();
                File.Delete(zipPath);
                throw new IOException("Isi zip pembaruan tidak lengkap (Launcher/Web tidak ada); dibuang.");
            }
        }
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        Directory.CreateDirectory(staging);
        ZipFile.ExtractToDirectory(zipPath, staging);
    }

    // ---- tahap 3: terapkan ----

    /// <summary>
    /// Dipanggil saat aplikasi baru dibuka (tanpa jaringan, cepat): bila ada pembaruan yang sudah diunduh lengkap, terapkan SEKARANG.
    /// Mengembalikan true bila penerapan dimulai (Shutdown() sudah diminta; pemanggil berhenti).
    /// </summary>
    public static bool TerapkanBilaSiap(ServerProcessManager server)
    {
        if (LewatiSesiIni) return false;
        var siap = BacaSiap();
        if (siap is null) return false;
        if (!Version.TryParse(NormalizeVersion(siap.Versi), out var v) || v <= Terpasang())
        {
            BersihkanSisaUpdate(); // sudah terpasang atau penanda rusak
            return false;
        }
        var status = BacaStatus();
        if (status is not null && status.Target == v.ToString() && status.Percobaan >= MaksPercobaan && (DateTime.UtcNow - status.TerakhirUtc) < TimeSpan.FromHours(24))
        {
            PesanTahan = PesanDitahan(v, Terpasang(), status.Percobaan);
            return false;
        }
        Log($"Pembaruan {siap.Versi} sudah diunduh sebelumnya: diterapkan saat aplikasi dibuka.");
        return TerapkanSekarang(server);
    }

    /// <summary>Menerapkan pembaruan yang sudah siap: matikan server, helper menyalin dari staging lalu membuka aplikasi baru.</summary>
    public static bool TerapkanSekarang(ServerProcessManager server)
    {
        var siap = BacaSiap();
        if (siap is null) { Log("Tidak ada pembaruan siap untuk diterapkan."); return false; }

        // Catat percobaan SEBELUM menerapkan: bila penyalinan gagal dan aplikasi lama terbuka lagi, percobaan ini tetap terhitung.
        var sebelumnya = BacaStatus();
        TulisStatus(new StatusPercobaan(siap.Versi, (sebelumnya is not null && sebelumnya.Target == siap.Versi ? sebelumnya.Percobaan : 0) + 1, DateTime.UtcNow));

        var installDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        var exePath = Path.Combine(installDir, "DataMaster.Launcher.exe");
        var stagingDir = siap.Staging;

        // Matikan proses ANAK (DataMaster.Web) SEKARANG: berkasnya (di bawah installDir\web\) ikut ditimpa, kuncinya harus lepas dulu.
        server.StopIntentionally();
        // Mode server: DataMaster.Web dijalankan WINDOWS SERVICE, jadi StopIntentionally() tidak menyentuhnya. Tanpa dimatikan EKSPLISIT, berkas web\ ditimpa
        // tetapi proses service LAMA hidup dgn kode lama di memori (bug nyata Data Master 2026-09-25). Tunggu sampai benar2 Stopped.
        if (WindowsServiceHelper.IsInstalled()) WindowsServiceHelper.StopDanTungguUntukUpdate();

        var pid = Process.GetCurrentProcess().Id;
        var scriptPath = Path.Combine(Path.GetTempPath(), "datamaster-update.bat");
        // robocopy (BUKAN xcopy; /IS = tetap timpa berkas yang tampak sama): mengulang otomatis bila berkas terkunci (/R:15 /W:2) dan KODE KELUARNYA diperiksa
        // (>= 8 = gagal). wwwroot SENGAJA TIDAK dibuang (foto pengguna disimpan di web\wwwroot\img). Semua langkah dicatat ke
        // update_helper.log. Bila gagal, aplikasi lama dibuka dgn --update-gagal (sesi itu tidak langsung mencoba lagi).
        var script =
            "@echo off\r\n" +
            "set \"LOGDIR=%LOCALAPPDATA%\\DataMaster\\logs\"\r\n" +
            "if not exist \"%LOGDIR%\" mkdir \"%LOGDIR%\"\r\n" +
            "set \"LOG=%LOGDIR%\\update_helper.log\"\r\n" +
            $"echo [%date% %time%] helper mulai, menunggu PID {pid} keluar >> \"%LOG%\"\r\n" +
            ":wait\r\n" +
            $"tasklist /FI \"PID eq {pid}\" 2>NUL | find \"{pid}\" >NUL\r\n" +
            "if not errorlevel 1 (\r\n" +
            "    ping 127.0.0.1 -n 2 >NUL\r\n" +
            "    goto wait\r\n" +
            ")\r\n" +
            $"robocopy \"{stagingDir}\" \"{installDir}\" /E /IS /R:15 /W:2 /NFL /NDL /NJH /NJS /NP >> \"%LOG%\" 2>&1\r\n" +
            "set \"RC=%ERRORLEVEL%\"\r\n" +
            "echo [%date% %time%] robocopy kode keluar %RC% >> \"%LOG%\"\r\n" +
            "if %RC% GEQ 8 goto gagal\r\n" +
            $"rmdir /S /Q \"{stagingDir}\"\r\n" +
            $"del /F /Q \"{BerkasSiap}\" 2>NUL\r\n" +
            "echo [%date% %time%] update diterapkan, membuka aplikasi baru >> \"%LOG%\"\r\n" +
            $"start \"\" \"{exePath}\"\r\n" +
            "goto selesai\r\n" +
            ":gagal\r\n" +
            "echo [%date% %time%] PENYALINAN GAGAL, berkas masih terkunci - membuka aplikasi lama >> \"%LOG%\"\r\n" +
            $"start \"\" \"{exePath}\" --update-gagal\r\n" +
            ":selesai\r\n" +
            "(goto) 2>NUL & del \"%~f0\"\r\n"; // idiom: helper menghapus dirinya tanpa pesan galat "batch file cannot be found"
        File.WriteAllText(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        });

        Log("Helper penerapan dijalankan, Shutdown() diminta.");
        Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
        return true;
    }

    private static void Log(string pesan)
    {
        try
        {
            var logDir = Path.Combine(DataLokal, "logs");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, $"update_{DateTime.Now:yyyy-MM-dd}.log"), $"[{DateTime.Now:O}] {pesan}\n");
        }
        catch { /* logging tidak boleh ikut melempar error baru */ }
    }

    private static string NormalizeVersion(string v)
    {
        var parts = v.Trim().Split('.');
        var padded = parts.Concat(Enumerable.Repeat("0", Math.Max(0, 4 - parts.Length))).Take(4);
        return string.Join(".", padded);
    }
}
