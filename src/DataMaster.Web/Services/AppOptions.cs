namespace DataMaster.Web.Services;

// Port dari variabel .env PHP asli - lihat 04-infra-auth-sync.md §10. InstallType
// menggantikan install_type()/effective_install_type() (session-based dev-preview
// override tetap dibaca terpisah di DevPreviewService, bukan di sini - kelas ini
// murni nilai KONFIGURASI, bukan nilai EFEKTIF setelah override).
public class AppOptions
{
    // Alamat RESMI Hub API (VPS pemilik proyek) - SATU-SATUNYA tempat nilai ini
    // ditulis di kode (dipakai AuthController utk bawaan Setup Awal, DAN
    // Program.cs utk migrasi otomatis appsettings.json PC yang SUDAH pernah
    // setup - lihat komentar Program.cs). Kalau suatu saat domain/VPS pindah:
    // 1) ubah nilai ini, 2) tambahkan alamat LAMA ke HubApiUrlLama di bawah,
    // 3) rilis versi baru - SEMUA PC (baru maupun yang sudah lama jalan) akan
    // otomatis ikut pindah sendiri lewat auto-update, TANPA staf mana pun
    // perlu edit apapun manual di PC-nya.
    public const string HubApiUrlResmi = "https://alikhlas86.duckdns.org/hub-api";

    // Riwayat alamat resmi SEBELUMNYA (kosong = belum pernah pindah) - dipakai
    // Program.cs utk mengenali "ini alamat bawaan LAMA, bukan alamat custom
    // yang sengaja diisi manual" sebelum menggantinya otomatis ke HubApiUrlResmi
    // di atas. JANGAN dihapus riwayatnya - biarkan menumpuk supaya PC yang baru
    // update setelah SEKIAN LAMA (lewat beberapa kali pindah VPS) tetap ikut
    // termigrasi dari alamat manapun yang pernah jadi bawaan resmi.
    public static readonly string[] HubApiUrlLama = [];

    // Kunci BERSAMA (bukan token per-unit) yang ditanam di SEMUA instalasi
    // Data Master - dipakai AuthController.Setup() memanggil POST /api/v1/register
    // Hub API SENDIRI (tanpa staf TU pernah lihat/ketik kata "token" sama sekali).
    // HARUS SAMA PERSIS dgn register.sharedKey di .env server Hub API. Kalau
    // bocor: paling parah orang bikin unit PALSU (gampang dihapus dari panel
    // admin), TIDAK BISA baca/ubah data unit lain manapun - lihat komentar
    // RegisterController.php (hub-api) utk penjelasan lengkap.
    public const string RegisterSharedKey = "84692fef821026acc9a8fb08a7f16e03636d3b1a3041093af19ab8a643e66037";

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

    // Versi Launcher yang SEDANG jalan (2026-09-16, keluhan nyata user -
    // "biar ga bingung sekarang versi berapa" setelah beberapa kali
    // upgrade/downgrade manual PC TU TK). Diisi Launcher lewat env var,
    // SAMA PERSIS pola LanMode dkk di atas - kosong kalau dijalankan
    // `dotnet run` langsung (bukan lewat Launcher, lihat Program.cs).
    public string AppVersion { get; set; } = "";
}
