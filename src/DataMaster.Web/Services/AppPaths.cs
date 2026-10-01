namespace DataMaster.Web.Services;

// Nama folder data di %LocalAppData% PER MODE (2026-10-01). Sebelumnya SEMUA
// mode (server, development) berbagi 1 folder "DataMaster" - database,
// hubapi.json (token Hub API produksi), fingerprint sinkron, passphrase
// backup. Akibatnya PC yang sebelumnya server lalu dipindah ke "development"
// membuka database ASLI dan tetap tersambung ke Hub produksi dgn token yang
// sama: data uji bisa terkirim (termasuk penghapusan) ke aplikasi orang tua &
// guru. Sekarang mode development (InstallType "pengembang", diteruskan
// Launcher lewat env AppSettings__InstallType) memakai folder "DataMaster-Dev"
// = sandbox kosong terpisah TOTAL, tanpa token. Folder lama "DataMaster" tetap
// dipakai server (data asli, tanpa migrasi). Harus sama persis dgn
// ServerProcessManager.DataDirectory di Launcher.
public static class AppPaths
{
    public static string DataFolderName =>
        string.Equals(Environment.GetEnvironmentVariable("AppSettings__InstallType"), "pengembang", StringComparison.OrdinalIgnoreCase)
            ? "DataMaster-Dev"
            : "DataMaster";

    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DataFolderName);
}
