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
        Port = isServerMode ? _config.ServerPort : GetFreeTcpPort();
        // BaseUrl (dipakai WebView2 PC INI sendiri + healthz check lokal) SELALU
        // loopback - Kestrel yang didengarkan ke 0.0.0.0 tetap menjawab di
        // 127.0.0.1 juga, jadi PC server tetap bisa memakai app-nya sendiri
        // normal spt mode mandiri. listenUrl (dipakai ASPNETCORE_URLS, yaitu
        // alamat yang benar2 "didengarkan" Kestrel) beda: 0.0.0.0 KHUSUS mode
        // "server" supaya PC lain di jaringan yang sama bisa menyambung.
        BaseUrl = $"http://127.0.0.1:{Port}";
        var listenUrl = isServerMode ? $"http://0.0.0.0:{Port}" : BaseUrl;

        Directory.CreateDirectory(DataDirectory);
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
