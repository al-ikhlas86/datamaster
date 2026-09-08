using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DataMaster.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Services;

// Port dari App\Libraries\DatabaseBackup (PHP) - lihat 04-infra-auth-sync.md §8.5.
// ADAPTASI SENGAJA: PHP asli pakai mysqldump/mysql shell (MySQL). Di sini SQLite -
// "dump" konsisten dipakai `VACUUM INTO` (snapshot atomik walau WAL aktif, setara
// mysqldump --single-transaction), "restore" = timpa file .db langsung (bukan
// terapkanSql() baris-per-baris). Format enkripsi backup awan (AES-256-CBC,
// PBKDF2-SHA256 100rb iterasi, marker "ARSIPV1") DIPERTAHANKAN PERSIS supaya
// konsep "kalimat sandi 16+ karakter, tidak ada fallback otomatis" tetap sama -
// lihat §8.2. Restore MEMBUTUHKAN restart aplikasi (bukan hot-swap ditengah
// koneksi EF Core aktif) - Launcher WPF yang menjalankan ulang proses.
public class DatabaseBackupService(DataMasterDbContext db, IConfiguration config, IHostApplicationLifetime lifetime)
{
    private const string Marker = "ARSIPV1";
    private const int Pbkdf2Iterations = 100_000;
    private const int ManualBackupMaks = 7;

    private string DbFilePath()
    {
        var cs = config.GetConnectionString("DataMaster") ?? "Data Source=App_Data/datamaster.db";
        var builder = new SqliteConnectionStringBuilder(cs);
        return Path.GetFullPath(builder.DataSource);
    }

    private string BackupDir()
    {
        var dir = Path.Combine(Path.GetDirectoryName(DbFilePath())!, "..", "backup");
        Directory.CreateDirectory(dir);
        return Path.GetFullPath(dir);
    }

    // backupManual() - simpan salinan POLOS (tanpa enkripsi) ke folder backup lokal,
    // rotasi maksimal 7 terbaru (bug nyata diperbaiki di PHP asli - tombol manual
    // dulu luput dari rotasi, lihat §1.6).
    public async Task<string> BuatBackupManualAsync()
    {
        var nama = $"manual_{DateTime.Now:yyyy-MM-dd_HHmmss}.db";
        var path = Path.Combine(BackupDir(), nama);
        await db.Database.ExecuteSqlAsync($"VACUUM INTO {path}");
        RotasiManual();
        await CatatWaktuAsync("last_backup_manual_at");
        return nama;
    }

    private void RotasiManual()
    {
        var files = Directory.GetFiles(BackupDir(), "manual_*.db").OrderByDescending(f => f).ToList();
        foreach (var f in files.Skip(ManualBackupMaks)) { try { File.Delete(f); } catch { /* biarkan, coba lagi siklus berikutnya */ } }
    }

    // ---------------------------------------------------------------- Backup Awan (terenkripsi)
    // Port dari App\Commands\BackupCloud.php - lihat 04-infra-auth-sync.md §8.2.
    private const int AntrianMaks = 7;

    private string AntrianDir()
    {
        var dir = Path.Combine(Path.GetDirectoryName(DbFilePath())!, "..", "backup-antrian");
        Directory.CreateDirectory(dir);
        return Path.GetFullPath(dir);
    }

    // buatBackupHariIni() - cek jam terjadwal (diatur Admin di Setting), idempotent
    // (kalau berkas hari ini SUDAH ada di antrian, dilewati - aman dijalankan
    // sesering apa pun). Return true kalau berkas baru benar2 dibuat.
    public async Task<bool> BuatBackupHariIniJikaPerluAsync(string passphrase)
    {
        var jamTerjadwal = await JamBackupOnlineAsync();
        if (DateTime.Now.Hour < jamTerjadwal) return false;

        var tanggal = DateTime.Now.ToString("yyyy-MM-dd");
        if (Directory.GetFiles(AntrianDir(), $"datamaster_{tanggal}_*.db.enc").Length > 0) return false;

        var tempPath = Path.Combine(Path.GetTempPath(), $"dm_dump_{Guid.NewGuid():N}.db");
        try
        {
            await db.Database.ExecuteSqlAsync($"VACUUM INTO {tempPath}");
            var plain = await File.ReadAllBytesAsync(tempPath);
            var encrypted = Enkripsi(plain, passphrase);
            var nama = $"datamaster_{DateTime.Now:yyyy-MM-dd_HHmmss}.db.enc";
            await File.WriteAllBytesAsync(Path.Combine(AntrianDir(), nama), encrypted);
        }
        finally
        {
            if (File.Exists(tempPath)) { try { File.Delete(tempPath); } catch { /* abaikan */ } }
        }

        RotasiAntrian();
        return true;
    }

    // Batas antrian ANTRIAN_MAKS=7 - jaga disk tidak menggunung kalau internet
    // mati berkepanjangan. Nama file sudah mengandung tanggal+jam, urut abjad =
    // urut waktu (sort()).
    private void RotasiAntrian()
    {
        var files = Directory.GetFiles(AntrianDir(), "*.db.enc").OrderBy(f => f).ToList();
        foreach (var f in files.Take(Math.Max(0, files.Count - AntrianMaks))) { try { File.Delete(f); } catch { /* abaikan */ } }
    }

    // kirimAntrian() - jalan TANPA syarat jam (beda dari buatBackupHariIni), kirim
    // SEMUA berkas di antrian urut file TERLAMA dulu. Gagal koneksi ATAU ditolak
    // server (non-exception, mis. token salah) -> STOP loop (jangan membanjiri
    // server dgn percobaan file berikutnya), berkas dibiarkan di antrian dicoba
    // lagi siklus berikutnya.
    public async Task<int> KirimAntrianAsync(HttpClient http, string url, string token)
    {
        var files = Directory.GetFiles(AntrianDir(), "*.db.enc").OrderBy(f => f).ToList();
        var terkirim = 0;

        foreach (var f in files)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                var bytes = await File.ReadAllBytesAsync(f);
                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(fileContent, "berkas", Path.GetFileName(f));

                using var req = new HttpRequestMessage(HttpMethod.Post, $"{url}/api/v1/backup/upload");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                req.Content = content;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                using var resp = await http.SendAsync(req, cts.Token);

                if (!resp.IsSuccessStatusCode) break;

                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                if (!doc.RootElement.TryGetProperty("success", out var ok) || !ok.GetBoolean()) break;

                File.Delete(f);
                terkirim++;
                await CatatWaktuAsync("last_backup_online_at");
            }
            catch
            {
                break; // internet mati/timeout - berhenti, coba lagi siklus berikutnya
            }
        }

        return terkirim;
    }

    public async Task CatatWaktuAsync(string key)
    {
        var existing = await db.SystemSettings.FindAsync(key);
        if (existing is null) db.SystemSettings.Add(new Data.Entities.SystemSetting { SettingKey = key, SettingValue = DateTime.Now.ToString("O"), UpdatedAt = DateTime.Now });
        else { existing.SettingValue = DateTime.Now.ToString("O"); existing.UpdatedAt = DateTime.Now; }
        await db.SaveChangesAsync();
    }

    public async Task<DateTime?> WaktuTerakhirAsync(string key)
    {
        var row = await db.SystemSettings.FindAsync(key);
        return row?.SettingValue is { } v && DateTime.TryParse(v, out var dt) ? dt : null;
    }

    public async Task<int> JamBackupOnlineAsync()
    {
        var row = await db.SystemSettings.FindAsync("backup_cloud_hour");
        return row?.SettingValue is { } v && int.TryParse(v, out var jam) && jam is >= 0 and <= 23 ? jam : 12;
    }

    public async Task SimpanJamBackupOnlineAsync(int jam)
    {
        var existing = await db.SystemSettings.FindAsync("backup_cloud_hour");
        if (existing is null) db.SystemSettings.Add(new Data.Entities.SystemSetting { SettingKey = "backup_cloud_hour", SettingValue = jam.ToString(), UpdatedAt = DateTime.Now });
        else { existing.SettingValue = jam.ToString(); existing.UpdatedAt = DateTime.Now; }
        await db.SaveChangesAsync();
    }

    // Format: "ARSIPV1" + salt(16) + iv(16) + ciphertext. AES-256-CBC, kunci turunan
    // PBKDF2-SHA256 100.000 iterasi - lihat §8.2. Salt & IV acak PER FILE.
    public static byte[] Enkripsi(byte[] plaintext, string passphrase)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var iv = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(passphrase), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);

        using var aes = Aes.Create();
        aes.KeySize = 256; aes.Key = key; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
        using var encryptor = aes.CreateEncryptor();
        var ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

        using var ms = new MemoryStream();
        ms.Write(Encoding.ASCII.GetBytes(Marker));
        ms.Write(salt);
        ms.Write(iv);
        ms.Write(ciphertext);
        return ms.ToArray();
    }

    public static byte[] Dekripsi(byte[] data, string passphrase)
    {
        var markerBytes = Encoding.ASCII.GetBytes(Marker);
        if (data.Length < markerBytes.Length + 32 || !data.AsSpan(0, markerBytes.Length).SequenceEqual(markerBytes))
            throw new InvalidDataException("Berkas bukan hasil enkripsi backup yang valid (marker ARSIPV1 tidak ditemukan).");

        var salt = data.AsSpan(markerBytes.Length, 16).ToArray();
        var iv = data.AsSpan(markerBytes.Length + 16, 16).ToArray();
        var ciphertext = data.AsSpan(markerBytes.Length + 32).ToArray();

        var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(passphrase), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);

        using var aes = Aes.Create();
        aes.KeySize = 256; aes.Key = key; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        try
        {
            return decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        }
        catch (CryptographicException)
        {
            throw new InvalidDataException("Gagal membuka berkas - sandi salah atau berkas rusak.");
        }
    }

    private static readonly byte[] SqliteHeader = "SQLite format 3\0"u8.ToArray();

    // Restore: timpa file .db langsung (bukan terapkanSql() baris-per-baris seperti
    // PHP - SQLite tidak butuh replay SQL, cukup ganti file utuh). WAJIB restart
    // proses setelahnya (StopApplication) - Launcher yang menjalankan ulang, migrasi
    // otomatis jalan lagi persis alur startup normal (lihat Program.cs).
    public async Task RestoreAsync(byte[] fileBytes, string? passphrase, bool terenkripsi)
    {
        var isi = terenkripsi ? Dekripsi(fileBytes, passphrase ?? "") : fileBytes;

        if (isi.Length < SqliteHeader.Length || !isi.AsSpan(0, SqliteHeader.Length).SequenceEqual(SqliteHeader))
            throw new InvalidDataException("Berkas hasil buka bukan database SQLite yang valid.");

        var dbPath = DbFilePath();
        // Salinan pengaman SEBELUM ditimpa - lapisan tambahan yang tidak ada di PHP
        // asli (mysqldump/restore berbeda karakter risiko), murni jaga-jaga supaya
        // restore yang keliru tidak menghilangkan data lama tanpa jejak sama sekali.
        var pengaman = Path.Combine(BackupDir(), $"pra_restore_{DateTime.Now:yyyy-MM-dd_HHmmss}.db");
        await db.Database.ExecuteSqlAsync($"VACUUM INTO {pengaman}");

        SqliteConnection.ClearAllPools();
        await File.WriteAllBytesAsync(dbPath, isi);

        lifetime.StopApplication();
    }
}
