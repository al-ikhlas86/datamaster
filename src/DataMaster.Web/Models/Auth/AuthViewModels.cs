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
    // ID login utama - SENGAJA bukan email (sekolah ini tidak benar-benar pakai
    // alamat email nyata utk akun-akun ini). Kolom Email di database tetap ADA
    // (NOT NULL di skema, warisan pola Myth Auth PHP) tapi diisi OTOMATIS dari
    // ID ini di AuthController.Setup() - pengguna TIDAK PERNAH melihat/mengisi
    // email sama sekali. Username tetap BISA diganti belakangan lewat menu
    // Pengaturan (UserController.Update() sudah mendukung ini).
    [Required(ErrorMessage = "ID wajib diisi.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "ID 3-30 karakter.")]
    [RegularExpression(@"^[a-zA-Z0-9\s]+$", ErrorMessage = "ID hanya boleh huruf, angka, dan spasi.")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Kata sandi wajib diisi.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Kata sandi minimal 8 karakter.")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Konfirmasi kata sandi wajib diisi.")]
    public string PasswordKonfirmasi { get; set; } = "";

    // Opsional - kalau diisi, langsung ditulis ke appsettings.json (AppSettings)
    // supaya PC ini otomatis sinkron ke Hub API sejak awal, TANPA perlu buka file
    // manual (lihat AuthController.Setup()). Boleh dikosongkan & diisi belakangan
    // lewat menu Pengaturan kalau token belum siap saat instalasi.
    public string? HubApiUrl { get; set; }
    public string? HubApiToken { get; set; }
}
