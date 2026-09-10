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
public class AuthController(DataMasterDbContext db, LoginThrottleService throttle, AppSettingsWriterService appSettingsWriter, IHostApplicationLifetime lifetime) : Controller
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
        var user = new User { Email = $"{uname.Replace(" ", "")}@datamaster.local", Username = uname, PasswordHash = "" };
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

        // Opsional - kalau token Hub API diisi sekalian pas Setup Awal, langsung
        // tulis ke appsettings.json (tidak perlu staf IT buka file manual) lalu
        // restart proses supaya AppOptions (IOptions<AppOptions>, TIDAK hot-reload)
        // kebaca ulang dari nilai baru sejak proses fresh - Launcher yang menjalankan
        // ulang otomatis, pola SAMA PERSIS restart-setelah-restore-database.
        var tokenDiisi = !string.IsNullOrWhiteSpace(input.HubApiToken);
        if (tokenDiisi)
        {
            await appSettingsWriter.SetHubApiConfigAsync(input.HubApiUrl, input.HubApiToken);
            TempData["message"] = "Akun admin berhasil dibuat. Menyalakan ulang sebentar untuk mengaktifkan sinkronisasi Hub API...";
            lifetime.StopApplication();
        }
        else
        {
            TempData["message"] = "Akun admin berhasil dibuat.";
        }

        return RedirectToAction("Index", "Home");
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
