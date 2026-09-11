using System.Security.Claims;
using DataMaster.Data;
using DataMaster.Data.Entities;
using DataMaster.Web.Models.Auth;
using DataMaster.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Controllers;

// Port dari alur login Myth Auth (vendor package, tidak punya controller sendiri di
// PHP asli - rute /login/logout otomatis dari vendor) - lihat 04-infra-auth-sync.md
// §1.2-1.4, §6. `allowRegistration=false` di PHP asli (akun HANYA dibuat manual admin
// via CLI `spark auth:create_user`) - CLI tidak masuk akal utk distribusi desktop
// end-user, jadi diganti "Setup Awal" 1 kali SAAT users KOSONG (bukan pendaftaran
// bebas kapan saja - begitu ada 1 user, layar setup tidak pernah muncul lagi,
// mencegah kelas bug yang sama yg mendasari allowRegistration=false di PHP: siapapun
// bisa daftar dapat akses penuh tanpa RBAC).
[AllowAnonymous]
[Route("")]
public class AuthController(DataMasterDbContext db, LoginThrottleService throttle, AppSettingsWriterService appSettingsWriter, IHostApplicationLifetime lifetime, HubApiRegistrationService hubApiRegistration) : Controller
{
    [HttpGet("login")]
    public async Task<IActionResult> Login()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");

        var adaUser = await db.Users.AnyAsync();
        if (!adaUser) return View("Setup", new SetupInput());

        return View(new LoginInput());
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginInput input)
    {
        if (!ModelState.IsValid) return View(input);

        var loginValue = (input.Login ?? "").Trim();
        var sisaTunggu = throttle.SisaTungguDetik(loginValue);
        if (sisaTunggu > 0)
        {
            ModelState.AddModelError("", $"Terlalu banyak percobaan gagal. Coba lagi dalam {sisaTunggu} detik.");
            return View(input);
        }

        var user = await db.Users.Include(u => u.GroupMemberships).ThenInclude(g => g.AuthGroup)
            .FirstOrDefaultAsync(u => u.Email == loginValue || u.Username == loginValue);

        var hasher = new PasswordHasher<User>();
        var passOk = user is not null && hasher.VerifyHashedPassword(user, user.PasswordHash, input.Password ?? "") != PasswordVerificationResult.Failed;

        if (user is null || !passOk)
        {
            throttle.CatatGagal(loginValue);
            ModelState.AddModelError("", "Email/username atau kata sandi salah.");
            return View(input);
        }
        if (!user.Active)
        {
            throttle.CatatGagal(loginValue);
            ModelState.AddModelError("", "Akun ini tidak aktif. Hubungi admin.");
            return View(input);
        }

        throttle.Reset(loginValue);
        await SignInAsync(user, input.IngatLogin);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost("login/setup")]
    public async Task<IActionResult> Setup(SetupInput input)
    {
        // Guard: HANYA boleh dipakai selagi users benar2 kosong - mencegah endpoint
        // ini dipakai sbg jalan belakang pendaftaran bebas kapan saja.
        if (await db.Users.AnyAsync())
        {
            TempData["error"] = "Akun admin sudah ada. Hubungi admin untuk dibuatkan akun baru.";
            return RedirectToAction(nameof(Login));
        }
        if (!ModelState.IsValid) return View(input);
        if (input.Password != input.PasswordKonfirmasi)
        {
            ModelState.AddModelError("", "Konfirmasi kata sandi tidak cocok.");
            return View(input);
        }

        var uname = input.Username.Trim();
        // Kolom Email SENGAJA disi otomatis (bukan diminta ke pengguna) - sekolah
        // ini tidak benar-benar pakai alamat email nyata utk akun-akun ini, ID
        // (Username) yang jadi identitas login utama. Kolom Email tetap harus
        // terisi (NOT NULL+UNIQUE di skema) supaya kompatibel dgn struktur data
        // warisan Myth Auth PHP tanpa perlu migrasi skema terpisah.
        var hasher = new PasswordHasher<User>();
        var kodePemulihan = RecoveryCodeService.Generate();
        var user = new User
        {
            Email = $"{uname.Replace(" ", "")}@datamaster.local",
            Username = uname,
            PasswordHash = "",
            RecoveryCodeHash = RecoveryCodeService.Hash(kodePemulihan),
        };
        user.PasswordHash = hasher.HashPassword(user, input.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var grupAdmin = await db.AuthGroups.FirstOrDefaultAsync(g => g.Name == "admin");
        if (grupAdmin is null)
        {
            grupAdmin = new AuthGroup { Name = "admin", Description = "Administrator" };
            db.AuthGroups.Add(grupAdmin);
            await db.SaveChangesAsync();
        }
        db.AuthGroupUsers.Add(new AuthGroupUser { UserId = user.UserId, AuthGroupId = grupAdmin.AuthGroupId });
        await db.SaveChangesAsync();

        await SignInAsync(user, ingatLogin: false);

        // Opsional - kalau Nama Unit diisi, DAFTARKAN OTOMATIS ke Hub API (POST
        // /api/v1/register) - staf TU tidak pernah lihat/ketik "token"/"alamat
        // server" sama sekali (lihat diskusi 2026-09-10, komentar SetupInput).
        // Token hasil registrasi ditulis sendiri ke appsettings.json, lalu proses
        // di-restart supaya AppOptions (IOptions<AppOptions>, TIDAK hot-reload)
        // kebaca ulang dari nilai baru - Launcher yang menjalankan ulang otomatis,
        // pola SAMA PERSIS restart-setelah-restore-database.
        var namaUnit = (input.NamaUnit ?? "").Trim();
        var perluRestart = false;
        if (namaUnit != "")
        {
            var token = await hubApiRegistration.DaftarAsync(namaUnit);
            if (token is not null)
            {
                await appSettingsWriter.SetHubApiConfigAsync(AppOptions.HubApiUrlResmi, token);
                TempData["message"] = "Akun admin berhasil dibuat. Menyalakan ulang sebentar untuk mengaktifkan sinkronisasi Hub API...";
                perluRestart = true;
            }
            else
            {
                // Non-fatal SENGAJA - kalau pendaftaran gagal (internet mati,
                // server Hub API tidak terjangkau, dsb), akun admin TETAP
                // berhasil dibuat. Sinkronisasi bisa diaktifkan lagi belakangan.
                TempData["message"] = "Akun admin berhasil dibuat. Pendaftaran sinkronisasi Hub API gagal (cek koneksi internet) - bisa dicoba lagi belakangan.";
            }
        }
        else
        {
            TempData["message"] = "Akun admin berhasil dibuat.";
        }

        // Kode Pemulihan WAJIB ditampilkan SEKALI di sini SEBELUM restart (kalau
        // ada) benar2 dipicu - lihat TampilkanKodePemulihan() yg menyimpan flag
        // restart via TempData supaya halaman kode-nya sendiri yang memicu
        // StopApplication() SETELAH pengguna klik lanjut, bukan sebelum sempat
        // membaca kodenya.
        TempData["kode_pemulihan"] = kodePemulihan;
        TempData["kode_pemulihan_konteks"] = "Akun admin Anda berhasil dibuat. SIMPAN kode ini di tempat aman (dicatat/dicetak) - ini SATU-SATUNYA cara memulihkan akses kalau nanti lupa password dan belum login di perangkat mana pun.";
        TempData["kode_pemulihan_perlu_restart"] = perluRestart;
        return RedirectToAction(nameof(TampilkanKodePemulihan));
    }

    [HttpGet("login/kode-pemulihan")]
    public IActionResult TampilkanKodePemulihan()
    {
        if (TempData["kode_pemulihan"] is not string kode)
        {
            return RedirectToAction(nameof(Login));
        }
        // TempData.Peek dipakai (bukan biarkan konsumsi otomatis) supaya nilai
        // ini SELAMAT lewat 1x refresh halaman tak sengaja, tapi tetap
        // dihapus eksplisit begitu tombol "Lanjutkan" ditekan (lihat
        // LanjutSetelahKodePemulihan) - kode SEKALI TAMPIL, bukan berulang.
        var konteks = TempData.Peek("kode_pemulihan_konteks") as string ?? "";
        TempData.Keep("kode_pemulihan");
        TempData.Keep("kode_pemulihan_perlu_restart");
        return View(new KodePemulihanViewModel { Kode = kode, PesanKonteks = konteks });
    }

    // Satu action dipakai bersama utk ketiga konteks (Setup Awal biasa, Setup
    // Awal+restart Hub API, reset via Lupa Password) - dibedakan lewat
    // User.Identity (Setup Awal sudah SignInAsync duluan, Lupa Password
    // BELUM login sama sekali krn justru baru mau login lagi).
    [HttpPost("login/kode-pemulihan/lanjut")]
    public IActionResult LanjutSetelahKodePemulihan()
    {
        var perluRestart = TempData["kode_pemulihan_perlu_restart"] as bool? ?? false;
        if (perluRestart)
        {
            lifetime.StopApplication();
            return Content("Menyalakan ulang untuk mengaktifkan sinkronisasi Hub API... Halaman ini akan otomatis kembali normal dalam beberapa detik, silakan tunggu lalu muat ulang.");
        }
        return User.Identity?.IsAuthenticated == true
            ? RedirectToAction("Index", "Home")
            : RedirectToAction(nameof(Login));
    }

    // ------------------------------------------------------- Lupa Password (#5)

    [HttpGet("login/lupa-password")]
    public IActionResult LupaPassword() => View(new LupaPasswordInput());

    [HttpPost("login/lupa-password")]
    public async Task<IActionResult> LupaPassword(LupaPasswordInput input)
    {
        if (!ModelState.IsValid) return View(input);
        if (input.PasswordBaru != input.PasswordBaruKonfirmasi)
        {
            ModelState.AddModelError("", "Konfirmasi kata sandi tidak cocok.");
            return View(input);
        }

        var loginValue = input.Login.Trim();
        // Throttle KUNCI KEAMANAN utama di sini - entropi kode (~83 bit) tidak
        // berarti apa2 kalau penyerang boleh coba tak terbatas. Reuse throttle
        // yang sama dgn login biasa (kunci gabungan "login-value", cukup krn
        // per-akun, bukan per-jenis-percobaan).
        var sisaTunggu = throttle.SisaTungguDetik(loginValue);
        if (sisaTunggu > 0)
        {
            ModelState.AddModelError("", $"Terlalu banyak percobaan gagal. Coba lagi dalam {sisaTunggu} detik.");
            return View(input);
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == loginValue || u.Username == loginValue);
        if (user is null || !RecoveryCodeService.Cocok(input.KodePemulihan, user.RecoveryCodeHash))
        {
            throttle.CatatGagal(loginValue);
            ModelState.AddModelError("", "ID/Username atau Kode Pemulihan salah.");
            return View(input);
        }

        throttle.Reset(loginValue);
        var hasher = new PasswordHasher<User>();
        user.PasswordHash = hasher.HashPassword(user, input.PasswordBaru);
        // Kode LAMA langsung tidak berlaku begitu dipakai (sekali pakai) -
        // digantikan kode BARU supaya tetap ada jalur pemulihan ke depan,
        // bukan dibiarkan kosong setelah dipakai sekali.
        var kodeBaru = RecoveryCodeService.Generate();
        user.RecoveryCodeHash = RecoveryCodeService.Hash(kodeBaru);
        await db.SaveChangesAsync();

        TempData["kode_pemulihan"] = kodeBaru;
        TempData["kode_pemulihan_konteks"] = "Password berhasil direset. Kode Pemulihan LAMA sudah tidak berlaku - ini kode PENGGANTINYA, SIMPAN di tempat aman untuk pemulihan berikutnya.";
        TempData["kode_pemulihan_perlu_restart"] = false;
        TempData["message"] = "Password berhasil direset. Silakan login dengan password baru Anda.";
        return RedirectToAction(nameof(TampilkanKodePemulihan));
    }

    [HttpPost("logout")]
    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private async Task SignInAsync(User user, bool ingatLogin)
    {
        var groups = await db.AuthGroupUsers.Where(g => g.UserId == user.UserId).Include(g => g.AuthGroup).Select(g => g.AuthGroup.Name).ToListAsync();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Username ?? user.Email),
            new(ClaimTypes.Email, user.Email),
        };
        claims.AddRange(groups.Select(g => new Claim(ClaimTypes.Role, g)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        // "Ingat login (30 hari)" - persis semangat allowRemembering/rememberLength
        // Myth Auth (lihat §1.2). Tanpa centang, cookie sesi biasa (habis browser ditutup).
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = ingatLogin,
            ExpiresUtc = ingatLogin ? DateTimeOffset.UtcNow.AddDays(30) : null,
        });
    }
}
