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
}
