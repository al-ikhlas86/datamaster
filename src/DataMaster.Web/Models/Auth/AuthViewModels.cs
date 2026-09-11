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

    // Opsional - kalau diisi, AuthController.Setup() otomatis MENDAFTARKAN unit
    // ini ke Hub API (POST /api/v1/register) & menyimpan token hasilnya sendiri -
    // staf TU TIDAK PERNAH lihat/ketik kata "token" atau "alamat server" sama
    // sekali (lihat diskusi 2026-09-10: alur lama - login admin panel + copy
    // token manual - dianggap terlalu teknis utk non-programmer). Boleh
    // dikosongkan kalau belum mau aktifkan sinkronisasi saat instalasi.
    [StringLength(100, ErrorMessage = "Nama unit maksimal 100 karakter.")]
    public string? NamaUnit { get; set; }
}

// Lupa Password (2026-09-11, poin #5) - lihat catatan lengkap di User.cs &
// RecoveryCodeService.cs. Satu-satunya jalur pemulihan kalau admin lupa
// password DAN belum login di perangkat mana pun.
public class LupaPasswordInput
{
    [Required(ErrorMessage = "ID/Username wajib diisi.")]
    public string Login { get; set; } = "";

    [Required(ErrorMessage = "Kode Pemulihan wajib diisi.")]
    public string KodePemulihan { get; set; } = "";

    [Required(ErrorMessage = "Kata sandi baru wajib diisi.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Kata sandi minimal 8 karakter.")]
    public string PasswordBaru { get; set; } = "";

    [Required(ErrorMessage = "Konfirmasi kata sandi wajib diisi.")]
    public string PasswordBaruKonfirmasi { get; set; } = "";
}

// Ditampilkan SEKALI (dibawa lewat TempData, hilang setelah 1x tampil) -
// setelah Setup Awal, setelah reset via Kode Pemulihan, atau setelah
// generate ulang manual dari Pengaturan.
public class KodePemulihanViewModel
{
    public required string Kode { get; set; }
    public required string PesanKonteks { get; set; }
}
