using System.Diagnostics;
using System.IO;
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

    // Tulis service-config.json (dibaca Program.cs DataMaster.Web - lihat catatan
    // lengkap di sana) LALU pasang & nyalakan service via sc.exe TERELEVASI
    // (1x UAC). Return true HANYA kalau instalasi+start benar2 berhasil -
    // pemanggil (ServerProcessManager) WAJIB fallback ke anak proses lama
    // kalau ini return false, JANGAN pernah anggap "sudah pasti jalan".
    public static bool TryInstallAndStart(string webExePath, string dataDirectory, int port)
    {
        try
        {
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
