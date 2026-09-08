using System.ComponentModel.DataAnnotations;

namespace DataMaster.Web.Models.Auth;

public class LoginInput
{
    [Required(ErrorMessage = "Email atau username wajib diisi.")]
    public string? Login { get; set; }

    [Required(ErrorMessage = "Kata sandi wajib diisi.")]
    public string? Password { get; set; }

    public bool IngatLogin { get; set; }
}

public class SetupInput
{
    [Required(ErrorMessage = "Email wajib diisi.")]
    [EmailAddress(ErrorMessage = "Format email tidak valid.")]
    public string Email { get; set; } = "";

    [StringLength(30, MinimumLength = 3, ErrorMessage = "Username 3-30 karakter.")]
    public string? Username { get; set; }

    [Required(ErrorMessage = "Kata sandi wajib diisi.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Kata sandi minimal 8 karakter.")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Konfirmasi kata sandi wajib diisi.")]
    public string PasswordKonfirmasi { get; set; } = "";
}
