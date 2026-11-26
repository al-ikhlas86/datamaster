using DataMaster.Data;
using DataMaster.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DataMaster.Tests;

// Menjaga migrasi database: SEMUA migrasi harus berjalan mulus dari database kosong, dan model C# harus sama dengan snapshot migrasi (tidak ada perubahan
// model yang lupa dibuatkan migrasinya). Migrasi yang rusak membuat aplikasi gagal menyala di PC sekolah, jadi diuji sebelum rilis.
public sealed class MigrasiTests : IDisposable
{
    private readonly string _berkas = Path.Combine(Path.GetTempPath(), $"dm-uji-{Guid.NewGuid():N}.db");

    private DataMasterDbContext Buka() => new(new DbContextOptionsBuilder<DataMasterDbContext>().UseSqlite($"Data Source={_berkas}").Options);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _berkas, _berkas + "-wal", _berkas + "-shm" }) { try { File.Delete(f); } catch (IOException) { } }
    }

    [Fact]
    public void Semua_migrasi_berjalan_dan_model_sama_dengan_snapshot()
    {
        using var db = Buka();
        db.Database.Migrate();
        Assert.Empty(db.Database.GetPendingMigrations());
        Assert.False(db.Database.HasPendingModelChanges(), "Model C# berbeda dari snapshot migrasi: ada perubahan yang belum dibuatkan migrasi.");
    }

    [Fact]
    public void Calon_siswa_dari_PSB_online_unik_per_id_tetapi_calon_biasa_boleh_banyak()
    {
        using var db = Buka();
        db.Database.Migrate();
        CalonSiswa Calon(string nama, int? psb) => new() { Nama = nama, JenisKelamin = JenisKelamin.L, PsbOnlineId = psb, PsbNomor = psb is null ? null : $"PSB-1-{psb:0000}" };

        db.CalonSiswa.AddRange(Calon("Manual 1", null), Calon("Manual 2", null), Calon("Online 7", 7));
        db.SaveChanges();
        Assert.Equal(3, db.CalonSiswa.Count());

        db.CalonSiswa.Add(Calon("Online 7 ganda", 7));
        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void Kolom_PSB_tersimpan_dan_terbaca_kembali()
    {
        using (var db = Buka())
        {
            db.Database.Migrate();
            db.CalonSiswa.Add(new CalonSiswa { Nama = "Budi", JenisKelamin = JenisKelamin.P, PsbOnlineId = 3, PsbNomor = "PSB-1-0003", PsbStatus = "diterima", PsbGelombang = "Gelombang 1", PsbJson = "{\"a\":1}", PsbDisinkronAt = new DateTime(2026, 11, 27, 8, 0, 0) });
            db.SaveChanges();
        }
        using var baca = Buka();
        var c = baca.CalonSiswa.Single(x => x.PsbOnlineId == 3);
        Assert.Equal(("PSB-1-0003", "diterima", "Gelombang 1", "{\"a\":1}"), (c.PsbNomor, c.PsbStatus, c.PsbGelombang, c.PsbJson));
        Assert.Equal(new DateTime(2026, 11, 27, 8, 0, 0), c.PsbDisinkronAt);
    }
}
