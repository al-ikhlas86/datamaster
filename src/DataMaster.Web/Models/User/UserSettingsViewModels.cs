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

    // Status Hub API (2026-09-11, diperluas 2026-09-14 jadi 3 nilai sejak
    // fitur persetujuan admin) - "belum" (belum diisi sama sekali),
    // "menyambungkan" (baru didaftarkan, belum ada hasil sync sama sekali
    // sejak restart), "pending" (token ada tapi Hub API MENOLAK - biasanya
    // sedang menunggu admin klik Setujui), "aktif" (sync sungguhan pernah
    // berhasil paling baru). Dihitung dari 2 timestamp yang dicatat
    // HubApiSyncService, BUKAN cuma "apakah config lokal terisi" - status
    // lama itu bisa BOHONG bilang "Aktif" padahal server menolak terus.
    public required string HubApiStatus { get; set; }

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
