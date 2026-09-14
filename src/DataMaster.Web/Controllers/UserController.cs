using System.Security.Claims;
using System.Text.RegularExpressions;
using DataMaster.Data;
using DataMaster.Web.Models.User;
using DataMaster.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DataMaster.Web.Controllers;

// Port 1:1 dari app/Controllers/User.php ("Setting" - gabungan Edit Profil +
// Ubah Password + Backup/Pemulihan sejak 2026-08-27) - lihat 04-infra-auth-sync.md
// §1.5-1.6. role:admin (satu2nya grup dipakai nyata di sistem asli).
[Authorize(Roles = "admin")]
[Route("user")]
public class UserController(DataMasterDbContext db, DatabaseBackupService backup, IOptions<AppOptions> appOptions, AppSettingsWriterService appSettingsWriter, HubApiRegistrationService hubApiRegistration, IHostApplicationLifetime lifetime) : Controller
{
    // REGEX_TEKS_PENDEK-setara utk username: alpha_numeric_space (BaseController.php
    // di PHP asli tidak dipakai di sini krn username punya aturan alpha_numeric_space
    // sendiri, bukan REGEX_NAMA/REGEX_TEKS_PENDEK - lihat §1.5 poin 1).
    private static readonly Regex UsernameRegex = new(@"^[a-zA-Z0-9\s]+$", RegexOptions.Compiled);

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        if (user is null) return RedirectToAction("Logout", "Auth");

        // Status Hub API JUJUR (2026-09-14, fitur persetujuan admin) - BUKAN
        // cuma "apakah config lokal terisi" (yang lama, bisa nampilin "Aktif"
        // padahal server MENOLAK tiap sync krn belum disetujui admin). Dilihat
        // dari 2 timestamp yang dicatat HubApiSyncService: yang PALING BARU
        // di antara kedua nilai itu yang menentukan status sungguhan saat ini.
        var hubApiTerkonfigurasi = !string.IsNullOrWhiteSpace(appOptions.Value.HubApiUrl) && !string.IsNullOrWhiteSpace(appOptions.Value.HubApiToken);
        var syncOkTerakhir = await backup.WaktuTerakhirAsync("last_hub_sync_ok_at");
        var syncDitolakTerakhir = await backup.WaktuTerakhirAsync("last_hub_sync_unauthorized_at");
        var hubApiStatus = !hubApiTerkonfigurasi ? "belum"
            : syncOkTerakhir is null && syncDitolakTerakhir is null ? "menyambungkan"
            : syncDitolakTerakhir > syncOkTerakhir ? "pending"
            : "aktif";

        var vm = new UserSettingsViewModel
        {
            UserId = user.UserId,
            Email = user.Email,
            Username = user.Username,
            UserImage = user.UserImage,
            Active = user.Active,
            LastBackupManualAt = await backup.WaktuTerakhirAsync("last_backup_manual_at"),
            LastBackupOnlineAt = await backup.WaktuTerakhirAsync("last_backup_online_at"),
            JamBackupOnline = await backup.JamBackupOnlineAsync(),
            LanMode = appOptions.Value.LanMode,
            LanHostname = appOptions.Value.LanHostname,
            LanPort = appOptions.Value.LanPort,
            HubApiStatus = hubApiStatus,
            BackupPassphraseAktif = !string.IsNullOrWhiteSpace(appOptions.Value.BackupPassphrase),
        };
        return View(vm);
    }

    [HttpPost("update")]
    public async Task<IActionResult> Update(string? username, IFormFile? user_image)
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        if (user is null) return RedirectToAction("Logout", "Auth");

        var uname = (username ?? "").Trim();
        var errors = new List<string>();
        if (uname == "") errors.Add("Username wajib diisi.");
        else if (uname.Length < 3 || uname.Length > 30) errors.Add("Username 3-30 karakter.");
        else if (!UsernameRegex.IsMatch(uname)) errors.Add("Username hanya boleh huruf, angka, dan spasi.");
        else if (await db.Users.AnyAsync(u => u.Username == uname && u.UserId != user.UserId)) errors.Add("Username ini sudah dipakai.");

        if (user_image is not null && user_image.Length > 0)
        {
            var ext = Path.GetExtension(user_image.FileName).ToLowerInvariant();
            if (ext is not (".jpg" or ".jpeg" or ".png")) errors.Add("Foto harus JPG atau PNG.");
            else if (user_image.Length > 500 * 1024) errors.Add("Ukuran foto maksimal 500KB.");
        }

        if (errors.Count > 0)
        {
            TempData["error"] = string.Join(" ", errors);
            return RedirectToAction(nameof(Index));
        }

        user.Username = uname;

        if (user_image is not null && user_image.Length > 0)
        {
            var imgDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "img");
            Directory.CreateDirectory(imgDir);
            var ext = Path.GetExtension(user_image.FileName).ToLowerInvariant();
            var namaBaru = $"{Guid.NewGuid():N}{ext}";
            await using (var stream = System.IO.File.Create(Path.Combine(imgDir, namaBaru)))
                await user_image.CopyToAsync(stream);

            if (!string.IsNullOrEmpty(user.UserImage) && user.UserImage != "default.png")
            {
                var lama = Path.Combine(imgDir, user.UserImage);
                if (System.IO.File.Exists(lama)) { try { System.IO.File.Delete(lama); } catch { /* abaikan, tidak fatal */ } }
            }
            user.UserImage = namaBaru;
        }

        await db.SaveChangesAsync();
        TempData["message"] = "Profil berhasil diperbarui.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("password")]
    public async Task<IActionResult> UpdatePassword(string? password_lama, string? password_baru, string? password_baru_konfirmasi)
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        if (user is null) return RedirectToAction("Logout", "Auth");

        var hasher = new PasswordHasher<Data.Entities.User>();
        if (string.IsNullOrEmpty(password_lama) || hasher.VerifyHashedPassword(user, user.PasswordHash, password_lama) == PasswordVerificationResult.Failed)
        {
            TempData["error"] = "Kata sandi lama tidak cocok.";
            return RedirectToAction(nameof(Index));
        }
        // Sesuai catatan §6/§19 spec: alur ganti password sendiri di PHP asli HANYA
        // menegakkan min_length[8]+matches (TIDAK menjalankan passwordValidators
        // Composition/NothingPersonal/Dictionary yang berlaku di reset password resmi
        // Myth Auth) - inkonsistensi yang SUDAH ADA di PHP asli, direplikasi apa adanya.
        if (string.IsNullOrEmpty(password_baru) || password_baru.Length < 8)
        {
            TempData["error"] = "Kata sandi baru minimal 8 karakter.";
            return RedirectToAction(nameof(Index));
        }
        if (password_baru != password_baru_konfirmasi)
        {
            TempData["error"] = "Konfirmasi kata sandi baru tidak cocok.";
            return RedirectToAction(nameof(Index));
        }

        user.PasswordHash = hasher.HashPassword(user, password_baru);
        await db.SaveChangesAsync();
        TempData["message"] = "Kata sandi berhasil diubah.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("backup-manual")]
    public async Task<IActionResult> BackupManual()
    {
        try
        {
            var nama = await backup.BuatBackupManualAsync();
            TempData["message"] = $"Backup manual berhasil dibuat: {nama}.";
        }
        catch (Exception ex)
        {
            TempData["error"] = $"Gagal membuat backup: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("jadwal-backup")]
    public async Task<IActionResult> SimpanJadwalBackup(string? jam_backup)
    {
        if (!int.TryParse(jam_backup, out var jam) || jam is < 0 or > 23)
        {
            // PHP: "Jam backup tidak valid - pilih antara 00:00 sampai 23:00." (User::simpanJadwalBackup).
            TempData["error"] = "Jam backup tidak valid - pilih antara 00:00 sampai 23:00.";
            return RedirectToAction(nameof(Index));
        }
        await backup.SimpanJamBackupOnlineAsync(jam);
        // PHP: sprintf('Jam Backup Online disimpan: %02d:00 WIB setiap hari.', $jam) - sertakan JAM
        // yang baru disimpan (bukan cuma pesan generik) supaya Admin dapat konfirmasi nilai efektif.
        TempData["message"] = $"Jam Backup Online disimpan: {jam:D2}:00 WIB setiap hari.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("restore")]
    public async Task<IActionResult> Restore(IFormFile? file, string? sandi_restore, string? konfirmasi_restore)
    {
        // Urutan pengecekan disamakan dgn PHP User::restore(): file dulu (getFile()+isValid()),
        // BARU cek "TIMPA" - beda urutan bisa mengubah pesan mana yg tampil kalau keduanya salah.
        if (file is null || file.Length == 0)
        {
            // PHP: "Berkas restore tidak valid atau gagal di-upload."
            TempData["error"] = "Berkas restore tidak valid atau gagal di-upload.";
            return RedirectToAction(nameof(Index));
        }
        if ((konfirmasi_restore ?? "").Trim().ToUpperInvariant() != "TIMPA")
        {
            // PHP: "Restore dibatalkan - ketik TIMPA persis untuk konfirmasi."
            TempData["error"] = "Restore dibatalkan - ketik TIMPA persis untuk konfirmasi.";
            return RedirectToAction(nameof(Index));
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var terenkripsi = ext == ".enc";
        if (ext is not (".db" or ".enc"))
        {
            TempData["error"] = "Format file harus .db atau .db.enc.";
            return RedirectToAction(nameof(Index));
        }
        if (terenkripsi && string.IsNullOrEmpty(sandi_restore))
        {
            TempData["error"] = "Sandi pemulihan wajib diisi untuk berkas terenkripsi.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            await backup.RestoreAsync(ms.ToArray(), sandi_restore, terenkripsi);
            // Aplikasi akan berhenti sesaat lagi (StopApplication) - Launcher yang
            // menjalankan ulang proses, migrasi otomatis jalan lagi seperti startup
            // normal. Redirect ini kemungkinan besar tidak akan sempat dirender.
            TempData["message"] = "Pemulihan berhasil diterapkan. Aplikasi akan dimulai ulang.";
        }
        catch (Exception ex)
        {
            TempData["error"] = $"Gagal memulihkan: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    // Sambungkan/daftarkan ulang ke Hub API (2026-09-11) - SEBELUM ini,
    // satu2nya cara mengisi HubApiUrl/HubApiToken adalah Setup Awal, yang
    // cuma tampil SEKALI selagi database kosong - kalau nilainya ke-reset
    // (mis. bug lama, sudah diperbaiki - lihat AppSettingsWriterService)
    // TIDAK ADA cara mengisi ulang lewat UI sama sekali. Reuse persis alur
    // yang sama dgn Setup Awal (HubApiRegistrationService).
    [HttpPost("hub-api/sambungkan")]
    public async Task<IActionResult> SambungkanHubApi(string? nama_unit)
    {
        var namaUnit = (nama_unit ?? "").Trim();
        if (namaUnit == "")
        {
            TempData["error"] = "Nama unit wajib diisi.";
            return RedirectToAction(nameof(Index));
        }

        var token = await hubApiRegistration.DaftarAsync(namaUnit);
        if (token is null)
        {
            TempData["error"] = "Gagal menyambungkan ke Hub API - cek koneksi internet, lalu coba lagi.";
            return RedirectToAction(nameof(Index));
        }

        await appSettingsWriter.SetHubApiConfigAsync(AppOptions.HubApiUrlResmi, token);
        // "Aktif" langsung TIDAK BENAR sejak fitur persetujuan admin (2026-09-14)
        // - pendaftaran mandiri sekarang SELALU masuk sbg "menunggu persetujuan"
        // dulu di Hub API (lihat RegisterController.php sisi server), sinkronisasi
        // baru sungguhan jalan setelah admin klik Setujui di panel Hub API.
        TempData["message"] = "Berhasil didaftarkan ke Hub API - MENUNGGU PERSETUJUAN admin dulu sebelum sinkronisasi mulai jalan. Menyalakan ulang sebentar...";
        lifetime.StopApplication();
        return RedirectToAction(nameof(Index));
    }

    // Isi kata sandi enkripsi backup awan (2026-09-12, BUG NYATA - lihat
    // UserSettingsViewModel.BackupPassphraseAktif utk kronologi lengkap: fitur
    // backup awan sudah ADA & benar sejak awal, tapi TIDAK ADA satu pun jalur
    // UI utk mengisi prasyaratnya - backup diam2 tidak pernah jalan di
    // instalasi manapun). Restart diperlukan (pola sama SambungkanHubApi) -
    // IOptions<AppOptions> singleton, tidak hot-reload.
    [HttpPost("backup-passphrase")]
    public async Task<IActionResult> SimpanBackupPassphrase(string? passphrase)
    {
        var nilai = (passphrase ?? "").Trim();
        if (nilai.Length < 16)
        {
            TempData["error"] = "Kata sandi backup minimal 16 karakter.";
            return RedirectToAction(nameof(Index));
        }

        await appSettingsWriter.SetBackupPassphraseAsync(nilai);
        TempData["message"] = "Kata sandi backup awan berhasil disimpan. Menyalakan ulang sebentar untuk mengaktifkannya...";
        lifetime.StopApplication();
        return RedirectToAction(nameof(Index));
    }

    // Generate ulang Kode Pemulihan (2026-09-11, poin #5) - dipakai kalau kode
    // lama hilang/lupa disimpan TAPI masih bisa login normal (beda dari alur
    // Lupa Password di AuthController yang justru dipakai saat TIDAK bisa
    // login). Kode LAMA otomatis tidak berlaku lagi begitu diganti (1 kode
    // aktif per akun, bukan bertumpuk). Reuse halaman tampil-sekali yang sama
    // dgn Setup Awal/Lupa Password lewat TempData - lintas controller aman
    // krn TempData disimpan di cookie/session, bukan per-controller.
    [HttpPost("kode-pemulihan/generate-ulang")]
    public async Task<IActionResult> GenerateUlangKodePemulihan()
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        if (user is null) return RedirectToAction("Logout", "Auth");

        var kodeBaru = RecoveryCodeService.Generate();
        user.RecoveryCodeHash = RecoveryCodeService.Hash(kodeBaru);
        await db.SaveChangesAsync();

        TempData["kode_pemulihan"] = kodeBaru;
        TempData["kode_pemulihan_konteks"] = "Kode Pemulihan baru berhasil dibuat. Kode LAMA (kalau ada) sudah tidak berlaku lagi - SIMPAN kode baru ini di tempat aman.";
        TempData["kode_pemulihan_perlu_restart"] = false;
        return RedirectToAction("TampilkanKodePemulihan", "Auth");
    }
}
