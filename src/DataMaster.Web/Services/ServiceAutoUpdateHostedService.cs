using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace DataMaster.Web.Services;

// Pembaruan otomatis TANPA membuka aplikasi (2026-10-23, diminta pemilik: PC server tanpa monitor
// tetap harus ter-update sendiri). Sebelumnya hanya UpdateChecker di Launcher (WPF) yang memeriksa
// rilis - artinya server baru ter-update kalau ada orang membuka Data Master di PC itu.
//
// Hanya aktif kalau proses ini SUNGGUHAN berjalan sebagai Windows Service (mode server) dan
// tata letak instalasi dikenali (<instalasi>\DataMaster.Launcher.exe + <instalasi>\web\...).
// Mode anak-proses Launcher / `dotnet run` / instalasi pengembang tidak tersentuh.
//
// Alur: cek rilis GitHub tiap 3 jam -> kalau lebih baru, tunggu jendela malam (default 22:00-05:00,
// atau paksa setelah 3 hari) -> unduh + periksa isi zip -> jalankan skrip PowerShell terpisah
// (Scheduled Task SYSTEM sekali jalan, supaya selamat saat layanan ini berhenti) yang: hentikan
// layanan -> cadangkan instalasi lama -> salin berkas baru -> nyalakan layanan -> tunggu /healthz.
// Kalau /healthz tidak menjawab, instalasi lama dipulihkan dari cadangan dan versi itu ditandai
// gagal (tidak dicoba terus-menerus). Semua langkah tercatat di
// %ProgramData%\DataMaster\update\autoupdate.log.
//
// Matikan: AppSettings__AutoUpdateServer=false. Ubah jendela: AppSettings__AutoUpdateJamMulai/JamSelesai.
public class ServiceAutoUpdateHostedService(IConfiguration config, ILogger<ServiceAutoUpdateHostedService> logger) : BackgroundService
{
    // Nama repo baru (bukan "datamaster-desktop" yang hanya hidup lewat pengalihan GitHub).
    private const string ApiLatestReleaseUrl = "https://api.github.com/repos/al-ikhlas86/datamaster/releases/latest";
    private const string ApiAssetUrlTemplate = "https://api.github.com/repos/al-ikhlas86/datamaster/releases/assets/{0}";
    private const string AssetName = "DataMaster-win-x64.zip";
    private const string ServiceName = "DataMasterWebService";
    private const string TaskName = "DataMasterAutoUpdate";
    private static readonly TimeSpan CekInterval = TimeSpan.FromHours(3);
    private static readonly TimeSpan BatasPaksa = TimeSpan.FromDays(3);
    private static readonly TimeSpan DelayAwal = TimeSpan.FromMinutes(3);

    private DateTime? _tertundaSejak;
    private string _updateRoot = "";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            if (!WindowsServiceHelpers.IsWindowsService()) return;
            if (string.Equals(config["AppSettings:AutoUpdateServer"], "false", StringComparison.OrdinalIgnoreCase)) return;
            if (string.Equals(config["AppSettings:InstallType"], "pengembang", StringComparison.OrdinalIgnoreCase)) return;

            var webDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
            var installDir = Directory.GetParent(webDir)?.FullName;
            if (installDir is null || !File.Exists(Path.Combine(installDir, "DataMaster.Launcher.exe")))
            {
                logger.LogInformation("AutoUpdate: tata letak instalasi tidak dikenali ({Dir}) - dinonaktifkan.", webDir);
                return;
            }

            _updateRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DataMaster", "update");
            Directory.CreateDirectory(_updateRoot);
            Catat($"Layanan mulai, versi terpasang {VersiTerpasang()}, instalasi {installDir}.");

            await Task.Delay(DelayAwal, ct);
            using var timer = new PeriodicTimer(CekInterval);
            do
            {
                try
                {
                    if (await SatuSiklusAsync(installDir, ct)) return; // skrip pembaruan berjalan: layanan akan dihentikan olehnya
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Catat($"Siklus gagal (dicoba lagi nanti): {ex.Message}");
                    logger.LogWarning(ex, "AutoUpdate: siklus gagal.");
                }
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // Pembaruan otomatis tidak boleh pernah menjatuhkan server.
            logger.LogError(ex, "AutoUpdate: berhenti karena galat tak terduga.");
        }
    }

    private static Version VersiTerpasang() => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0, 0);

    // true = skrip pembaruan sudah dijalankan.
    private async Task<bool> SatuSiklusAsync(string installDir, CancellationToken ct)
    {
        var terpasang = VersiTerpasang();
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("DataMaster-AlIkhlas86-ServiceUpdater");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        string releaseJson;
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            cts.CancelAfter(TimeSpan.FromSeconds(20));
            releaseJson = await http.GetStringAsync(ApiLatestReleaseUrl, cts.Token);
        }
        using var doc = JsonDocument.Parse(releaseJson);
        var tag = doc.RootElement.GetProperty("tag_name").GetString();
        if (!AutoUpdatePolicy.TryParseTag(tag, out var remote) || remote <= terpasang)
        {
            _tertundaSejak = null;
            return false;
        }

        if (File.Exists(Path.Combine(_updateRoot, $"gagal-{remote}.flag")))
        {
            return false; // versi ini pernah gagal/dipulihkan di PC ini - tunggu rilis yang lebih baru
        }

        _tertundaSejak = BacaTertundaSejak(remote);
        var jamMulai = int.TryParse(config["AppSettings:AutoUpdateJamMulai"], out var jm) ? jm : 22;
        var jamSelesai = int.TryParse(config["AppSettings:AutoUpdateJamSelesai"], out var js) ? js : 5;
        if (!AutoUpdatePolicy.BolehTerapkan(DateTime.Now, _tertundaSejak, BatasPaksa, jamMulai, jamSelesai))
        {
            Catat($"Versi {remote} tersedia (terpasang {terpasang}); menunggu jendela {jamMulai:00}:00-{jamSelesai:00}:00.");
            return false;
        }

        long assetId = 0;
        long assetSize = 0;
        foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() == AssetName)
            {
                assetId = asset.GetProperty("id").GetInt64();
                assetSize = asset.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                break;
            }
        }
        if (assetId == 0)
        {
            Catat($"Rilis {tag} belum punya asset {AssetName} - dilewati.");
            return false;
        }

        var rootPath = Path.GetPathRoot(_updateRoot);
        if (!string.IsNullOrEmpty(rootPath) && new DriveInfo(rootPath).AvailableFreeSpace < 2L * 1024 * 1024 * 1024)
        {
            Catat("Ruang disk kurang dari 2 GB - pembaruan ditunda.");
            return false;
        }

        Catat($"Mengunduh {terpasang} -> {remote} ({assetSize / 1024 / 1024} MB)...");
        var zipPath = Path.Combine(_updateRoot, $"DataMaster-{remote}.zip");
        using (var req = new HttpRequestMessage(HttpMethod.Get, string.Format(ApiAssetUrlTemplate, assetId)))
        {
            req.Headers.Accept.ParseAdd("application/octet-stream");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMinutes(30));
            using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            resp.EnsureSuccessStatusCode();
            await using var src = await resp.Content.ReadAsStreamAsync(cts.Token);
            await using var dst = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await src.CopyToAsync(dst, 81920, cts.Token);
        }

        var staging = Path.Combine(_updateRoot, "staging");
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        Directory.CreateDirectory(staging);
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            var nama = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!nama.Contains("DataMaster.Launcher.exe") || !nama.Contains("web/DataMaster.Web.exe"))
            {
                Catat("Isi zip tidak lengkap (Launcher/Web tidak ada) - dibatalkan.");
                File.Delete(zipPath);
                File.WriteAllText(Path.Combine(_updateRoot, $"gagal-{remote}.flag"), "zip tidak lengkap");
                return false;
            }
        }
        ZipFile.ExtractToDirectory(zipPath, staging, overwriteFiles: true);
        File.Delete(zipPath);

        var scriptPath = Path.Combine(_updateRoot, "apply-update.ps1");
        File.WriteAllText(scriptPath, BuatSkrip(installDir, staging, remote.ToString(), PortLayanan()), new UTF8Encoding(true));
        Catat($"Menjalankan skrip pembaruan ke {remote} (layanan akan berhenti sebentar).");
        JalankanTerpisah(scriptPath);
        return true;
    }

    // Disimpan di berkas: PC yang dimatikan/reboot tiap hari tidak boleh mengulang hitungan batas paksa dari nol.
    private DateTime BacaTertundaSejak(Version remote)
    {
        var path = Path.Combine(_updateRoot, $"menunggu-{remote}.txt");
        try
        {
            if (File.Exists(path) && DateTime.TryParse(File.ReadAllText(path).Trim(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var sejak))
            {
                return sejak;
            }
            var sekarang = DateTime.Now;
            File.WriteAllText(path, sekarang.ToString("o"));
            return sekarang;
        }
        catch
        {
            return _tertundaSejak ?? DateTime.Now;
        }
    }

    private int PortLayanan()
    {
        // ASPNETCORE_URLS=http://0.0.0.0:5250 (diset Launcher lewat registri Environment layanan).
        var urls = config["ASPNETCORE_URLS"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "";
        foreach (var u in urls.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = u.LastIndexOf(':');
            if (idx > 0 && int.TryParse(u[(idx + 1)..].TrimEnd('/'), out var p)) return p;
        }
        return 5000;
    }

    private string BuatSkrip(string installDir, string staging, string versi, int port)
    {
        static string Q(string s) => s.Replace("'", "''");
        var backup = installDir + ".cadangan";
        var tpl = """
            $ErrorActionPreference = 'Continue'
            $install = '__INSTALL__'; $staging = '__STAGING__'; $backup = '__BACKUP__'
            $svc = '__SERVICE__'; $port = __PORT__; $log = '__LOG__'; $versi = '__VERSI__'; $flag = '__FLAG__'
            function Tulis($t) { Add-Content -Path $log -Value ('[{0:s}] {1}' -f (Get-Date), $t) -Encoding UTF8 }
            function Henti() {
                Stop-Service -Name $svc -Force -ErrorAction SilentlyContinue
                try { (Get-Service -Name $svc).WaitForStatus('Stopped', '00:01:00') } catch { }
                Get-Process -Name 'DataMaster.Launcher','DataMaster.Web' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
                Start-Sleep -Seconds 2
            }
            $sudahSalin = $false
            try {
                Tulis "== Mulai pembaruan ke $versi"
                Start-Sleep -Seconds 3
                Henti
                if (Test-Path -LiteralPath $backup) { Remove-Item -LiteralPath $backup -Recurse -Force -ErrorAction SilentlyContinue }
                robocopy $install $backup /MIR /R:2 /W:2 /NFL /NDL /NJH /NJS /NP | Out-Null
                if ($LASTEXITCODE -ge 8) { throw "cadangan gagal (robocopy $LASTEXITCODE)" }
                $sudahSalin = $true
                robocopy $staging $install /E /IS /IT /R:3 /W:3 /NFL /NDL /NJH /NJS /NP | Out-Null
                if ($LASTEXITCODE -ge 8) { throw "penyalinan gagal (robocopy $LASTEXITCODE)" }
                Start-Service -Name $svc
                $ok = $false
                for ($i = 0; $i -lt 40 -and -not $ok; $i++) {
                    Start-Sleep -Seconds 3
                    try { if ((Invoke-WebRequest -Uri "http://127.0.0.1:$port/healthz" -UseBasicParsing -TimeoutSec 5).StatusCode -eq 200) { $ok = $true } } catch { }
                }
                if (-not $ok) { throw "healthz tidak menjawab setelah pembaruan" }
                Tulis "== SUKSES: versi $versi berjalan, healthz OK"
            } catch {
                Tulis "== GAGAL: $_"
                Set-Content -Path $flag -Value "$_" -Encoding UTF8
                if ($sudahSalin) {
                    try {
                        Henti
                        robocopy $backup $install /MIR /R:3 /W:3 /NFL /NDL /NJH /NJS /NP | Out-Null
                        Tulis "Pemulihan dari cadangan selesai (robocopy $LASTEXITCODE)"
                    } catch { Tulis "PEMULIHAN GAGAL: $_" }
                }
                try { Start-Service -Name $svc } catch { Tulis "Gagal menyalakan layanan: $_" }
            } finally {
                Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
                schtasks.exe /Delete /TN '__TASK__' /F | Out-Null
            }
            """;
        return tpl
            .Replace("__INSTALL__", Q(installDir)).Replace("__STAGING__", Q(staging)).Replace("__BACKUP__", Q(backup))
            .Replace("__SERVICE__", ServiceName).Replace("__PORT__", port.ToString())
            .Replace("__LOG__", Q(Path.Combine(_updateRoot, "autoupdate.log")))
            .Replace("__VERSI__", Q(versi))
            .Replace("__FLAG__", Q(Path.Combine(_updateRoot, $"gagal-{versi}.flag")))
            .Replace("__TASK__", TaskName);
    }

    // Scheduled Task SYSTEM sekali jalan: tidak ikut mati saat layanan ini dihentikan. Cadangan: proses langsung.
    private void JalankanTerpisah(string scriptPath)
    {
        var perintah = $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"";
        if (Jalankan("schtasks.exe", "/Create", "/TN", TaskName, "/TR", perintah, "/SC", "ONCE", "/ST", "00:00", "/RU", "SYSTEM", "/RL", "HIGHEST", "/F")
            && Jalankan("schtasks.exe", "/Run", "/TN", TaskName))
        {
            return;
        }
        Catat("Scheduled Task gagal - menjalankan skrip langsung.");
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }

    private static bool Jalankan(string exe, params string[] args)
    {
        var psi = new ProcessStartInfo { FileName = exe, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi);
        if (p is null) return false;
        p.WaitForExit(30000);
        return p.HasExited && p.ExitCode == 0;
    }

    private void Catat(string pesan)
    {
        try { File.AppendAllText(Path.Combine(_updateRoot, "autoupdate.log"), $"[{DateTime.Now:s}] {pesan}{Environment.NewLine}"); }
        catch { /* mencatat tidak boleh melempar galat baru */ }
    }
}
