using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;

namespace DataMaster.Launcher;

// Windows Service utk DataMaster.Web (2026-09-12) - permintaan eksplisit user
// setelah dibandingkan dgn sistem lain (VAST/SQL Server): "PC nyala + terhubung
// jaringan = client langsung bisa masuk, TANPA perlu buka aplikasi Data Master
// sama sekali di PC server". Sebelumnya server cuma ANAK PROSES Launcher -
// tertutup Launcher = server ikut mati.
//
// SEKALI PASANG (butuh admin sekali - 1x UAC saat instalasi/upgrade PERTAMA
// stlh fitur ini ada), SETERUSNYA permanen: service otomatis nyala saat PC
// boot, survive logoff/restart, TIDAK PERNAH butuh siapapun buka aplikasi lagi.
//
// PENTING - fallback wajib: kalau pemasangan gagal (UAC ditolak, sc.exe error,
// dst), Launcher HARUS tetap bisa jalan pakai cara LAMA (anak proses) - PC
// yang sudah hidup produksi (PC TK, sudah ada data asli) tidak boleh sampai
// mati total gara2 fitur baru ini gagal separuh jalan.
public static class WindowsServiceHelper
{
    public const string ServiceName = "DataMasterWebService";

    public static bool IsInstalled()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            _ = sc.Status; // melempar InvalidOperationException kalau service tidak ada
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsRunning()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            sc.Refresh();
            return sc.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false;
        }
    }

    // Bandingkan binPath TERDAFTAR service (bisa basi - lihat catatan panjang
    // di ServerProcessManager.TryPakaiWindowsService) terhadap exe instalasi
    // PC ini SEKARANG. `sc qc` dipakai (bukan System.Management/WMI, supaya
    // tidak menambah dependency baru) - baris keluarannya persis
    // "        BINARY_PATH_NAME   : <path>".
    public static bool BinPathCocok(string webExePathSekarang)
    {
        var terdaftar = BacaBinPathTerdaftar();
        return terdaftar is not null
            && string.Equals(terdaftar.Trim(), webExePathSekarang.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? BacaBinPathTerdaftar()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"qc {ServiceName}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return null;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            const string marker = "BINARY_PATH_NAME";
            var line = output.Split('\n').FirstOrDefault(l => l.Contains(marker));
            if (line is null) return null;
            var idx = line.IndexOf(':');
            return idx < 0 ? null : line[(idx + 1)..].Trim();
        }
        catch
        {
            return null;
        }
    }

    // Service SUDAH ada tapi menunjuk exe yang SALAH (basi) - arahkan ulang
    // (bukan delete+create, `sc config` cukup & tidak mengganggu recovery
    // options yang sudah dipasang TryInstallAndStart sebelumnya) LALU restart
    // supaya proses lama (exe basi) benar2 diganti proses baru (exe benar).
    public static bool PerbaikiBinPathDanMulai(string webExePathBenar, int port, string connectionString, string? hubApiUrl, string? hubApiToken)
    {
        try
        {
            if (!RunElevated("sc.exe", $"config {ServiceName} binPath= \"{webExePathBenar}\"")) return false;
            TerapkanEnvironment(port, connectionString, hubApiUrl, hubApiToken);
            RunElevated("sc.exe", $"stop {ServiceName}");
            using (var scWait = new ServiceController(ServiceName))
            {
                try { scWait.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10)); } catch { /* mungkin sudah stop */ }
            }
            if (!RunElevated("sc.exe", $"start {ServiceName}")) return false;
            using var sc = new ServiceController(ServiceName);
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
            return sc.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false;
        }
    }

    // Env var yang SAMA PERSIS dipakai jalur anak proses (ServerProcessManager.
    // StartAsync) - satu2nya cara RESMI Windows mengirim env var ke sebuah
    // Windows Service adalah registry "Environment" (REG_MULTI_SZ) di key
    // service ybs, dibaca otomatis oleh SCM saat menyalakan prosesnya
    // (didokumentasikan Microsoft, bukan hack). WAJIB ADA - lihat BUG NYATA
    // 2026-09-16 (PC TU TK) di komentar panjang ServerProcessManager.StartAsync:
    // TANPA ini, service jalan sbg akun SYSTEM yang py %LocalAppData% SENDIRI
    // (C:\Windows\System32\config\systemprofile\...) TOTAL BEDA dari akun
    // interaktif - "SELF-KONFIGURASI" fallback di Program.cs (baca
    // %LocalAppData%\DataMaster miliknya SENDIRI) jadi PERCUMA, service diam2
    // pakai port default ASP.NET Core (bukan ServerPort) & database kosong di
    // lokasi yang salah - BUKAN soal drive/partisi apa pun, murni beda akun
    // Windows. reg.exe REG_MULTI_SZ via /d pakai "\0" literal sbg pemisah antar
    // string (perilaku terdokumentasi reg.exe, bukan escape sembarangan).
    public static bool TerapkanEnvironment(int port, string connectionString, string? hubApiUrl, string? hubApiToken)
    {
        var vars = new System.Collections.Generic.List<string>
        {
            $"ASPNETCORE_URLS=http://0.0.0.0:{port}",
            "ASPNETCORE_ENVIRONMENT=Production",
            $"ConnectionStrings__DataMaster={connectionString}",
        };
        if (!string.IsNullOrEmpty(hubApiUrl)) vars.Add($"AppSettings__HubApiUrl={hubApiUrl}");
        if (!string.IsNullOrEmpty(hubApiToken)) vars.Add($"AppSettings__HubApiToken={hubApiToken}");

        var data = string.Join("\\0", vars);
        return RunElevated("reg.exe", $"add \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\{ServiceName}\" /v Environment /t REG_MULTI_SZ /d \"{data}\" /f");
    }

    // Dipanggil ServerProcessManager kalau service TERPASANG & SCM bilang
    // "Running" tapi /healthz tidak pernah menjawab. Stop di sini SEBELUM
    // fallback anak proses supaya port ServerPort (tetap, sama persis dipakai
    // kedua jalur) benar2 bebas - tanpa ini fallback bisa gagal lagi krn
    // "address already in use".
    public static void StopUntukFallback()
    {
        try { RunElevated("sc.exe", $"stop {ServiceName}"); } catch { /* non-fatal - fallback tetap dicoba walau stop gagal */ }
    }

    public static void EnsureStarted()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            sc.Refresh();
            if (sc.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending)
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
            }
        }
        catch { /* non-fatal - StartAsync pemanggil tetap akan polling /healthz, gagal jelas terlihat dari situ */ }
    }

    // Pasang & nyalakan service via sc.exe TERELEVASI (1x UAC), suntikkan
    // port/connection string/token Hub API lewat registry Environment
    // (lihat TerapkanEnvironment). Return true HANYA kalau instalasi+start
    // benar2 berhasil - pemanggil (ServerProcessManager) WAJIB fallback ke
    // anak proses lama kalau ini return false, JANGAN pernah anggap "sudah
    // pasti jalan".
    public static bool TryInstallAndStart(string webExePath, string dataDirectory, int port, string connectionString, string? hubApiUrl, string? hubApiToken)
    {
        try
        {
            // service-config.json TIDAK LAGI dibaca Program.cs (2026-09-16,
            // BUG NYATA - lokasi ini dihitung dari %LocalAppData% akun
            // INTERAKTIF, sedangkan proses service jalan sbg SYSTEM yang py
            // %LocalAppData% sendiri, jadi tidak akan pernah ketemu file ini).
            // Tetap ditulis apa adanya sbg CATATAN/diagnostik manual saja -
            // sumber kebenaran SEKARANG adalah registry Environment lewat
            // TerapkanEnvironment di bawah.
            Directory.CreateDirectory(dataDirectory);
            var serviceConfigPath = Path.Combine(dataDirectory, "service-config.json");
            File.WriteAllText(serviceConfigPath, $$"""{"Port": {{port}}}""");

            // sc.exe create BUTUH quote ganda persis di sekitar binPath (spasi di
            // "Program Files" dkk) - format SPESIFIK sc.exe: `binPath= "..."`
            // (spasi setelah "=" WAJIB, kuirk lama sc.exe, BUKAN typo).
            var createArgs = $"create {ServiceName} binPath= \"{webExePath}\" start= auto DisplayName= \"Data Master - Server Data Sekolah\"";
            if (!RunElevated("sc.exe", createArgs)) return false;

            // Recovery: restart otomatis kalau service crash - jaring pengaman
            // tambahan (mis. exception tak tertangani di request tertentu tidak
            // boleh mematikan akses SELURUH sekolah sampai ada yang sadar & buka
            // manual, beda dari App WPF yang dulu setidaknya masih kelihatan
            // "tertutup" di taskbar).
            RunElevated("sc.exe", $"failure {ServiceName} reset= 86400 actions= restart/5000/restart/30000/restart/60000");

            TerapkanEnvironment(port, connectionString, hubApiUrl, hubApiToken);

            if (!RunElevated("sc.exe", $"start {ServiceName}")) return false;

            // sc start bersifat async (Service Control Manager) - tunggu benar2
            // Running sebelum dianggap sukses, supaya WaitUntilHealthyAsync
            // pemanggil tidak langsung nembak /healthz ke service yg masih boot.
            using var sc = new ServiceController(ServiceName);
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
            return sc.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false;
        }
    }

    // Proses TERPISAH (bukan Verb=runas pada sc.exe langsung) SENGAJA supaya
    // exit code sc.exe bisa dibaca (RunAs+ShellExecute tidak mengizinkan
    // redirect/exit-code yang reliable di semua versi Windows) - dijalankan via
    // cmd.exe /c yang ITU SENDIRI yang di-runas, sc.exe di dalamnya warisan
    // elevasi cmd induknya.
    private static bool RunElevated(string exe, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{exe} {args}\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit(15000);
            return p.HasExited && p.ExitCode == 0;
        }
        catch
        {
            // Termasuk kasus UAC DITOLAK user (Win32Exception "The operation was
            // canceled by the user") - dianggap gagal, pemanggil fallback ke cara
            // lama, BUKAN error fatal yang menghentikan aplikasi.
            return false;
        }
    }
}
