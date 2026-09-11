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
}
