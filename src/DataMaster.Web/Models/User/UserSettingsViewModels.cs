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
}
