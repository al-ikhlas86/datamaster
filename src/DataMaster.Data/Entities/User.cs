namespace DataMaster.Data.Entities;

// users - port dari tabel Myth Auth PHP. Grup yang BENAR-BENAR dipakai di sistem
// asli hanya 'admin' (lihat 04-infra-auth-sync.md §1.1) - AuthGroups/AuthPermissions
// tetap direplikasi untuk kesetiaan skema, tapi UI awal cukup fokus ke grup admin.
// PasswordHash pakai algoritma hashing .NET standar (mis. PBKDF2/Identity hasher),
// BUKAN wajib bit-identik dgn PHP password_hash() - user PHP lama tidak bisa
// dipindah otomatis, harus reset password saat migrasi pertama kali (didiskusikan
// terpisah, bukan bagian skema).
public class User
{
    public int UserId { get; set; }
    public required string Email { get; set; }
    public string? Username { get; set; }
    public required string PasswordHash { get; set; }
    public string? UserImage { get; set; }
    public bool Active { get; set; } = true;
    public bool ForcePassReset { get; set; }
    // Kode Pemulihan (2026-09-11, poin #5) - jalan keluar SATU-SATUNYA kalau lupa
    // password DAN belum login di mana pun (dialami LANGSUNG oleh pemilik proyek
    // sendiri sesi ini - sebelum ini TIDAK ADA jalur pemulihan sama sekali).
    // BUKAN OTP asli via SMS/WA - app desktop mandiri per-PC ini tidak punya
    // infrastruktur pengiriman (nomor HP admin bahkan tidak pernah dikumpulkan),
    // jadi dipakai kode pemulihan offline SEKALI PAKAI (ditampilkan SEKALI saat
    // dibuat - saat Setup Awal & tiap kali di-generate ulang dari Pengaturan -
    // user WAJIB menyimpannya sendiri, mis. dicetak/ditulis). Hash SAJA yang
    // disimpan (SHA-256, pola sama token Hub API) - kode asli tidak pernah
    // disimpan & TIDAK BISA dipulihkan kalau hilang, cuma bisa di-generate ulang
    // (otomatis menggantikan yang lama, bukan menambah).
    public string? RecoveryCodeHash { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<AuthGroupUser> GroupMemberships { get; set; } = new List<AuthGroupUser>();
}

// auth_groups - hanya 'admin' yang dipakai nyata di PHP asli, tapi tabel tetap
// ada untuk kesetiaan skema RBAC. PENTING: PHP asli TIDAK punya unique index di
// `name` - pernah menyebabkan bug nyata 4 grup "admin" duplikat karena script pakai
// INSERT IGNORE alih-alih WHERE NOT EXISTS (lihat 04-infra-auth-sync.md §12). Di
// SQLite WAJIB pasang UNIQUE di Name sejak awal supaya kelas bug ini tidak mungkin
// terjadi - lihat konfigurasi DbContext.
public class AuthGroup
{
    public int AuthGroupId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    public ICollection<AuthGroupUser> Members { get; set; } = new List<AuthGroupUser>();
}

public class AuthGroupUser
{
    public int UserId { get; set; }
    public int AuthGroupId { get; set; }

    public User User { get; set; } = null!;
    public AuthGroup AuthGroup { get; set; } = null!;
}

// system_settings - key/value generik, port dari tabel yang sama di PHP (dipakai
// DatabaseBackup::catatWaktu/waktuTerakhir untuk last_backup_manual_at,
// last_backup_online_at, backup_cloud_hour). Lihat 04-infra-auth-sync.md §1.6, §8.5.
public class SystemSetting
{
    public required string SettingKey { get; set; }
    public string? SettingValue { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
