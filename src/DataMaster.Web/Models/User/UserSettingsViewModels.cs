namespace DataMaster.Web.Models.User;

public class UserSettingsViewModel
{
    public int UserId { get; set; }
    public required string Email { get; set; }
    public string? Username { get; set; }
    public string? UserImage { get; set; }
    public bool Active { get; set; }
    public DateTime? LastBackupManualAt { get; set; }
    public DateTime? LastBackupOnlineAt { get; set; }
    public int JamBackupOnline { get; set; }
    public string LanMode { get; set; } = "mandiri";
    public string LanHostname { get; set; } = "";
    public int LanPort { get; set; }

    // Status Hub API (2026-09-11) - supaya staf tahu apakah sinkronisasi
    // sedang aktif TANPA perlu buka file/tanya siapa pun, dan bisa
    // sambungkan/daftarkan ulang sendiri kapan saja kalau ternyata kosong
    // (lihat AppSettingsWriterService utk kronologi kenapa ini bisa kosong
    // sendiri - dulu tidak ada UI sama sekali utk memperbaikinya).
    public bool HubApiAktif { get; set; }

    // BUG NYATA ditemukan 2026-09-12 lewat audit VPS: BackupCloudHostedService
    // SENGAJA menolak jalan sama sekali tanpa BackupPassphrase (lihat
    // komentarnya, minimal 16 karakter) - TAPI tidak ada satu pun halaman di
    // seluruh aplikasi yang bisa mengisinya, cuma bisa lewat edit manual file
    // config. Akibatnya: backup awan terenkripsi TIDAK PERNAH sekali pun
    // berhasil dibuat di instalasi manapun sejak fitur ini ada - dibuktikan
    // nyata, folder penampung di Hub API kosong total. HANYA status boolean
    // ini yang ditampilkan (bukan nilai sandinya) - begitu terisi, tidak ada
    // alasan sah menampilkannya lagi ke layar.
    public bool BackupPassphraseAktif { get; set; }
}
