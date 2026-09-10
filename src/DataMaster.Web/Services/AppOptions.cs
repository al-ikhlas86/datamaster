namespace DataMaster.Web.Services;

// Port dari variabel .env PHP asli - lihat 04-infra-auth-sync.md §10. InstallType
// menggantikan install_type()/effective_install_type() (session-based dev-preview
// override tetap dibaca terpisah di DevPreviewService, bukan di sini - kelas ini
// murni nilai KONFIGURASI, bukan nilai EFEKTIF setelah override).
public class AppOptions
{
    public string InstallType { get; set; } = "pendidikan"; // pendidikan|perusahaan|pengembang
    public string HubApiUrl { get; set; } = "";
    public string HubApiToken { get; set; } = "";
    public string BackupPassphrase { get; set; } = "";

    // Diisi Launcher (WPF) lewat environment variable saat mode LAN "server" -
    // supaya staf TU yang lupa alamat PC klien tidak perlu buka lagi wizard
    // setup awal (yang cuma tampil SEKALI), cukup lihat halaman Setting setelah
    // login (lihat User/Index.cshtml). Kosong/"mandiri" di instalasi biasa.
    public string LanMode { get; set; } = "mandiri";
    public string LanHostname { get; set; } = "";
    public int LanPort { get; set; }
}
