using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace DataMaster.Launcher;

// Menjalankan DataMaster.Web sbg PROSES ANAK terpisah (bukan hosting in-process) -
// port Kestrel dipilih dinamis (bebas konflik), koneksi database diarahkan ke
// folder data PER-PC di luar folder instalasi aplikasi (%LocalAppData%\DataMaster)
// supaya AMAN dari ditimpa saat auto-update, persis semangat arsitektur PHP asli
// (WRITEPATH/database terpisah dari kode aplikasi) - lihat komentar Program.cs
// DataMaster.Web ("Launcher akan meng-override connection string ini").
public sealed class ServerProcessManager : IDisposable
{
    private Process? _process;
    private bool _intentionalStop;
    private StreamWriter? _logWriter;
    private readonly LauncherConfig _config = LauncherConfig.Load();

    public int Port { get; private set; }

    // Mode "klien": tidak pernah menyalakan proses server sendiri sama sekali -
    // BaseUrl langsung menunjuk PC "server" lain di jaringan lokal yang sama
    // (lihat LauncherConfig.Mode). Mode lain (mandiri/server): PC ini sendiri
    // yang menyalakan server, BaseUrl SELALU 127.0.0.1 (loopback tetap jalan
    // walau Kestrel-nya didengarkan ke 0.0.0.0 juga utk mode "server").
    public bool IsKlien => _config.Mode == "klien";
    public string BaseUrl { get; private set; } = "";

    // Dipicu kalau proses server berhenti TANPA diminta Launcher - satu2nya
    // penyebab sah saat ini: DatabaseBackupService.RestoreAsync() memanggil
    // StopApplication() setelah restore diterapkan (lihat 04-infra-auth-sync.md,
    // catatan modul Auth/Setting). MainWindow merespons dgn restart otomatis.
    public event Action? ServerExitedUnexpectedly;

    public string DataDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DataMaster");

    public async Task<bool> StartAsync(CancellationToken ct)
    {
        if (IsKlien)
        {
            // Mode klien: TIDAK menyalakan proses server sama sekali - PC ini
            // cuma jendela yang menampilkan PC "server" lain di jaringan lokal
            // yang sama (lihat LauncherConfig.KlienServerUrl). Deadline lebih
            // panjang (60d, bukan 30d) drpd mode lokal krn PC server bisa saja
            // baru dinyalakan/masih boot bareng PC klien ini.
            _intentionalStop = false;
            BaseUrl = (_config.KlienServerUrl ?? "").TrimEnd('/');
            if (BaseUrl.Length == 0) return false;
            return await WaitUntilHealthyAsync(ct, checkLocalProcessAlive: false, timeoutSeconds: 60);
        }

        // Bersihkan proses & handle log SEBELUMNYA dulu - PENTING utk alur restart
        // (server berhenti sendiri stlh restore, lihat ServerExitedUnexpectedly).
        // Tanpa ini, StreamWriter kedua ke berkas log HARI YANG SAMA akan gagal
        // IOException (handle pertama belum dilepas krn FileShare.Read menolak
        // penulis kedua) - bug nyata ditemukan saat uji restart end-to-end:
        // StartAsync() gagal diam2 di tengah async void, server tidak pernah
        // benar2 menyala ulang meski proses Launcher sendiri tetap hidup.
        _process?.Dispose();
        _process = null;
        _logWriter?.Dispose();
        _logWriter = null;

        _intentionalStop = false;
        var isServerMode = _config.Mode == "server";
        Directory.CreateDirectory(DataDirectory);

        // Windows Service (2026-09-12, poin "server harus nyala sendiri tanpa
        // buka aplikasi") - HANYA utk mode "server" (PC yang benar2 dipakai PC
        // klien LAIN di jaringan). Mode "mandiri" (individu, 1 PC, TIDAK ada
        // klien lain yang bergantung) SENGAJA TIDAK PERNAH lewat jalur ini -
        // kalau app-nya ditutup di PC mandiri, memang tidak ada yang perlu
        // dilayani lagi, jadi tidak ada alasan menambah kerumitan/risiko UAC
        // & Service Control Manager utk PC seperti itu. Bug nyata ditemukan:
        // sebelum guard `isServerMode` ini, instalasi Individu di laptop dev
        // ikut lewat jalur Windows Service, dan kalau PC itu KEBETULAN sudah
        // punya `DataMasterWebService` basi dari instalasi/pengujian lain
        // (path exe beda), TryPakaiWindowsService percaya begitu saja bahwa
        // service basi itu representasi yang benar (WindowsServiceHelper.IsRunning()
        // cuma cek STATUS, tidak cek exe mana yang sebenarnya didengarkan) -
        // healthz tidak pernah menjawab, "Gagal Start" 30 detik. Lihat juga
        // WindowsServiceHelper.TryPakaiOrPerbaiki() utk pertahanan tambahan
        // (deteksi mismatch binPath) khusus kasus PC Server itu sendiri.
        if (isServerMode && TryPakaiWindowsService())
        {
            return await WaitUntilHealthyAsync(ct, checkLocalProcessAlive: false, timeoutSeconds: 30);
        }

        // --- Fallback: cara LAMA (anak proses) - dipertahankan APA ADANYA
        // supaya PC yang gagal dipasangi service (mis. UAC ditolak/sc.exe
        // error) TETAP BISA DIPAKAI, bukan mati total. ---
        Port = isServerMode ? _config.ServerPort : GetFreeTcpPort();
        // BaseUrl (dipakai WebView2 PC INI sendiri + healthz check lokal) SELALU
        // loopback - Kestrel yang didengarkan ke 0.0.0.0 tetap menjawab di
        // 127.0.0.1 juga, jadi PC server tetap bisa memakai app-nya sendiri
        // normal spt mode mandiri. listenUrl (dipakai ASPNETCORE_URLS, yaitu
        // alamat yang benar2 "didengarkan" Kestrel) beda: 0.0.0.0 KHUSUS mode
        // "server" supaya PC lain di jaringan yang sama bisa menyambung.
        BaseUrl = $"http://127.0.0.1:{Port}";
        var listenUrl = isServerMode ? $"http://0.0.0.0:{Port}" : BaseUrl;

        var logDir = Path.Combine(DataDirectory, "logs");
        Directory.CreateDirectory(logDir);
        LogCleanup.RotasiLogLama(logDir);

        var (fileName, arguments, workDir) = LocateServerExecutable();

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        // ASPNETCORE_URLS / ConnectionStrings__DataMaster - pola override config
        // via environment variable standar .NET (hierarki "__" = ":" di appsettings).
        // Database SENGAJA diletakkan di subfolder "App_Data" (BUKAN langsung di
        // DataDirectory) - DatabaseBackupService/HubApiSyncService menghitung
        // folder backup/backup-antrian/sync_state via "../" RELATIF ke folder
        // database (meniru tata letak dev App_Data/datamaster.db yang sudah diuji),
        // supaya logic itu tidak perlu tahu/berubah sama sekali soal siapa yang
        // menjalankannya (dotnet run langsung vs Launcher) - lihat bug nyata yang
        // ditemukan saat uji Launcher: taruh db LANGSUNG di root DataDirectory
        // membuat "../backup" salah naik ke luar folder DataMaster sama sekali.
        var appDataDir = Path.Combine(DataDirectory, "App_Data");
        Directory.CreateDirectory(appDataDir);
        psi.EnvironmentVariables["ASPNETCORE_URLS"] = listenUrl;
        psi.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Production";
        psi.EnvironmentVariables["ConnectionStrings__DataMaster"] = $"Data Source={Path.Combine(appDataDir, "datamaster.db")}";
        // Diteruskan ke halaman Setting (User/Index.cshtml) - supaya alamat PC
        // klien tetap bisa dilihat/disalin ulang kapan saja SETELAH login, tidak
        // cuma sekali muncul di wizard setup awal (keluhan nyata: staf TU lupa
        // catat, wizard itu sendiri tidak muncul lagi sesudahnya).
        psi.EnvironmentVariables["AppSettings__LanMode"] = _config.Mode;
        psi.EnvironmentVariables["AppSettings__LanHostname"] = Environment.MachineName;
        psi.EnvironmentVariables["AppSettings__LanPort"] = Port.ToString();

        // Fix BUG NYATA 2026-09-11: HubApiUrl/HubApiToken SEBELUMNYA ditulis
        // AppSettingsWriterService langsung ke web\appsettings.json (DI DALAM
        // folder instalasi) - file itu ADA di source control & DIBUNDLE ulang
        // di SETIAP rilis (nilai bawaannya kosong), jadi auto-update (xcopy /Y
        // menimpa SEMUA file) diam2 MERESET token Hub API balik ke kosong tiap
        // kali update terpasang - ditemukan nyata: instalasi TKIT yang sudah
        // sukses sinkron sebelumnya, setelah beberapa kali auto-update, token-nya
        // ternyata sudah kosong lagi tanpa siapa pun menyadari. Sekarang dibaca
        // dari file TERPISAH di DataDirectory (bukan folder instalasi, AMAN dari
        // ditimpa update - pola SAMA PERSIS ConnectionStrings di atas) sbg
        // override lewat environment variable - MENANG di atas nilai appsettings.json
        // bawaan apa pun yang datang dari rilis baru. AppSettingsWriterService.cs
        // (DataMaster.Web) sudah diubah menulis ke file yang SAMA ini.
        var hubApiConfigPath = Path.Combine(DataDirectory, "hubapi.json");
        if (File.Exists(hubApiConfigPath))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(hubApiConfigPath));
                if (doc.RootElement.TryGetProperty("HubApiUrl", out var u) && u.ValueKind == System.Text.Json.JsonValueKind.String)
                    psi.EnvironmentVariables["AppSettings__HubApiUrl"] = u.GetString();
                if (doc.RootElement.TryGetProperty("HubApiToken", out var tok) && tok.ValueKind == System.Text.Json.JsonValueKind.String)
                    psi.EnvironmentVariables["AppSettings__HubApiToken"] = tok.GetString();
            }
            catch { /* file rusak/tidak valid - biarkan appsettings.json bawaan yang berlaku, non-fatal */ }
        }

        _logWriter = new StreamWriter(File.Open(Path.Combine(logDir, $"web_{DateTime.Now:yyyy-MM-dd}.log"), FileMode.Append, FileAccess.Write, FileShare.Read)) { AutoFlush = true };

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) => { if (e.Data is not null) _logWriter.WriteLine(e.Data); };
        _process.ErrorDataReceived += (_, e) => { if (e.Data is not null) _logWriter.WriteLine(e.Data); };
        _process.Exited += (_, _) =>
        {
            if (!_intentionalStop) ServerExitedUnexpectedly?.Invoke();
        };

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        return await WaitUntilHealthyAsync(ct, checkLocalProcessAlive: true, timeoutSeconds: 30);
    }

    // Coba jalur Windows Service (2026-09-12, HANYA dipanggil utk mode
    // "server" - lihat StartAsync) - lihat catatan panjang di
    // WindowsServiceHelper.cs & StartAsync di atas. Return false kalau jalur
    // ini TIDAK berhasil disiapkan sama sekali (service belum ada & gagal
    // dipasang, atau proses instalasi service.exe tidak ditemukan) - pemanggil
    // WAJIB lanjut ke fallback anak proses, BUKAN anggap sukses.
    private bool TryPakaiWindowsService()
    {
        // Path exe DataMaster.Web YANG SEHARUSNYA dipakai instalasi PC ini
        // SEKARANG - dihitung DULU (bukan belakangan) supaya bisa dibandingkan
        // ke binPath service yang MUNGKIN SUDAH ada, lihat komentar mismatch
        // di bawah.
        string webExePath;
        try
        {
            var (fileName, _, _) = LocateServerExecutable();
            if (!fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false; // mode dev (dotnet <dll>) - service butuh exe asli
            webExePath = fileName;
        }
        catch
        {
            return false;
        }

        if (WindowsServiceHelper.IsInstalled())
        {
            // Bug nyata (2026-09-12): service yang SUDAH ada BELUM TENTU
            // menunjuk ke exe instalasi PC ini - bisa basi dari pengujian/
            // instalasi lain di path folder berbeda (mis. laptop dev yang
            // punya beberapa folder Data Master). Kalau dibiarkan, Launcher
            // akan mengira service itu representasi yang benar (statusnya
            // memang "Running"), padahal itu proses ASING - healthz tidak
            // pernah cocok/menjawab, "Gagal Start" 30 detik tanpa penjelasan
            // jelas ke user. Deteksi & benahi (arahkan ulang) SEBELUM percaya
            // status Running/Installed apa pun.
            if (!WindowsServiceHelper.BinPathCocok(webExePath))
            {
                return WindowsServiceHelper.PerbaikiBinPathDanMulai(webExePath, _config.ServerPort)
                    && SetelahServiceSiap();
            }

            WindowsServiceHelper.EnsureStarted();
            if (WindowsServiceHelper.IsRunning()) return SetelahServiceSiap();
            return false; // terpasang tapi gagal nyala - fallback, jangan paksa
        }

        // Belum pernah dipasang sama sekali - ini titik SEKALI SAJA yang
        // memicu 1x prompt UAC (instalasi pertama fitur ini, atau PC yang
        // baru pertama kali install Data Master versi ini). Kalau ditolak/
        // gagal, TryInstallAndStart return false & StartAsync lanjut fallback
        // - PC tetap bisa dipakai seperti sebelumnya, cuma belum dapat manfaat
        // "server nyala sendiri" sampai instalasi service diulang lain kali.
        return WindowsServiceHelper.TryInstallAndStart(webExePath, DataDirectory, _config.ServerPort)
            && SetelahServiceSiap();
    }

    private bool SetelahServiceSiap()
    {
        Port = _config.ServerPort;
        BaseUrl = $"http://127.0.0.1:{Port}";
        return true;
    }

    private async Task<bool> WaitUntilHealthyAsync(CancellationToken ct, bool checkLocalProcessAlive, int timeoutSeconds)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (checkLocalProcessAlive && _process is { HasExited: true }) return false; // gagal start - jangan tunggu penuh
            try
            {
                using var resp = await http.GetAsync($"{BaseUrl}/healthz", ct);
                if (resp.IsSuccessStatusCode) return true;
            }
            catch
            {
                // server belum siap menerima koneksi (atau, mode klien: PC server
                // belum menyala/jaringan belum tersambung) - coba lagi
            }
            await Task.Delay(300, ct);
        }
        return false;
    }

    // Dipanggil saat window ditutup pengguna - membedakan "berhenti krn diminta"
    // dari "berhenti sendiri" (restore) supaya tidak salah restart sesudah user
    // benar2 menutup aplikasi.
    public void StopIntentionally()
    {
        _intentionalStop = true;
        try { if (_process is { HasExited: false }) _process.Kill(entireProcessTree: true); } catch { /* proses mungkin sudah berhenti sendiri */ }
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    // Susunan folder PRODUKSI yang diasumsikan (dibangun oleh proses publish/CI -
    // lihat rencana CI/CD di spec/00-INDEX.md): Launcher.exe di root, DataMaster.Web
    // ter-publish ke subfolder "web/". Fallback DEV: proyek DataMaster.Web
    // ditemukan lewat struktur solusi relatif, dijalankan via `dotnet <dll>`.
    private static (string FileName, string Arguments, string WorkDir) LocateServerExecutable()
    {
        var baseDir = AppContext.BaseDirectory;

        var publishedWebFolder = Path.Combine(baseDir, "web", "DataMaster.Web.exe");
        if (File.Exists(publishedWebFolder)) return (publishedWebFolder, "", Path.GetDirectoryName(publishedWebFolder)!);

        var flatExe = Path.Combine(baseDir, "DataMaster.Web.exe");
        if (File.Exists(flatExe)) return (flatExe, "", baseDir);

        var dir = new DirectoryInfo(baseDir);
        for (var i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
        {
            foreach (var config in new[] { "Debug", "Release" })
            {
                var devDll = Path.Combine(dir.FullName, "DataMaster.Web", "bin", config, "net10.0", "DataMaster.Web.dll");
                if (File.Exists(devDll)) return ("dotnet", $"\"{devDll}\"", Path.GetDirectoryName(devDll)!);
            }
        }

        throw new FileNotFoundException("Tidak dapat menemukan DataMaster.Web (belum di-build/publish). Jalankan 'dotnet build' pada solusi DataMaster terlebih dahulu.");
    }

    public void Dispose()
    {
        StopIntentionally();
        _logWriter?.Dispose();
    }
}
