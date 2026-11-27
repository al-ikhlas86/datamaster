using System.Text.Json;
using DataMaster.Data;
using DataMaster.Data.Entities;
using DataMaster.Data.Psb;
using DataMaster.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DataMaster.Tests;

// Tagihan awal calon PSB dari Keuangan (2026-11-28): aturan murni, pembacaan snapshot, blokir Terima, dan penempatan kelas acak.
public sealed class TagihanAwalTests : IDisposable
{
    private readonly string _berkas = Path.Combine(Path.GetTempPath(), $"dm-awal-{Guid.NewGuid():N}.db");
    private static readonly IReadOnlyDictionary<string, string> Tingkat = new Dictionary<string, string>();

    private DataMasterDbContext Buka() => new(new DbContextOptionsBuilder<DataMasterDbContext>().UseSqlite($"Data Source={_berkas}").Options);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _berkas, _berkas + "-wal", _berkas + "-shm" }) { try { File.Delete(f); } catch (IOException) { } }
    }

    private static JsonElement Json(string t) => JsonDocument.Parse(t).RootElement.Clone();

    // ---------- aturan murni ----------
    [Theory]
    [InlineData(null, true)]
    [InlineData("tanpa_tagihan", true)]
    [InlineData("lunas", true)]
    [InlineData("belum", false)]
    [InlineData("sebagian", false)]
    public void Hanya_belum_dan_sebagian_memblokir_masuk_kelas(string? status, bool boleh) => Assert.Equal(boleh, PsbTagihanAwal.BolehMasukKelas(status));

    // ---------- snapshot ----------
    [Fact]
    public void Snapshot_membawa_tagihan_awal_dan_membuang_nilai_rusak()
    {
        var ok = PsbOnlineMapper.Baca(Json("""{"psb_id":1,"nomor":"P-1","nama":"A","tagihan_awal":{"status":"sebagian","total":5000000,"dibayar":2000000}}"""), Tingkat)!;
        Assert.Equal(("sebagian", 5000000m, 2000000m), (ok.TagihanStatus, ok.TagihanTotal, ok.TagihanDibayar));

        var tanpa = PsbOnlineMapper.Baca(Json("""{"psb_id":1,"nomor":"P-1","nama":"A","tagihan_awal":null}"""), Tingkat)!;
        Assert.Null(tanpa.TagihanStatus);

        var aneh = PsbOnlineMapper.Baca(Json("""{"psb_id":1,"nomor":"P-1","nama":"A","tagihan_awal":{"status":"gratis","total":-5,"dibayar":"x"}}"""), Tingkat)!;
        Assert.Null(aneh.TagihanStatus); // status tak dikenal = tidak ada kabar (jangan memblokir/meloloskan secara keliru)

        var nominalRusak = PsbOnlineMapper.Baca(Json("""{"psb_id":1,"nomor":"P-1","nama":"A","tagihan_awal":{"status":"lunas","total":-5,"dibayar":"x"}}"""), Tingkat)!;
        Assert.Equal("lunas", nominalRusak.TagihanStatus);
        Assert.Null(nominalRusak.TagihanTotal);
        Assert.Null(nominalRusak.TagihanDibayar);
    }

    // ---------- blokir Terima ----------
    private async Task<(int calonId, int kelasId)> SiapkanAsync(string? statusTagihan, int? psbId = 5)
    {
        using var db = Buka();
        db.Database.Migrate();
        var kelas = new Kelas { NamaKelas = "1A", Tingkat = "1" };
        var calon = new CalonSiswa { Nama = "Ahmad", JenisKelamin = JenisKelamin.L, PsbOnlineId = psbId, PsbTagihanStatus = statusTagihan, PsbTagihanTotal = 5_000_000m, PsbTagihanDibayar = 1_000_000m, TingkatDituju = "1" };
        db.Kelas.Add(kelas); db.CalonSiswa.Add(calon);
        await db.SaveChangesAsync();
        return (calon.CalonSiswaId, kelas.KelasId);
    }

    [Theory]
    [InlineData("belum")]
    [InlineData("sebagian")]
    public async Task Terima_ditolak_selama_tagihan_awal_belum_lunas(string status)
    {
        var (calonId, kelasId) = await SiapkanAsync(status);
        using var db = Buka();
        var hasil = await new PsbService(db).TerimaAsync(calonId, null, kelasId);
        Assert.False(hasil.Success);
        Assert.Contains("belum lunas", hasil.Message);
        Assert.Equal(StatusCalonSiswa.menunggu, (await db.CalonSiswa.FindAsync(calonId))!.Status);
        Assert.Equal(0, await db.Siswa.CountAsync());
    }

    [Theory]
    [InlineData("lunas", 5)]
    [InlineData(null, 5)]           // Keuangan belum memberi kabar: tidak memblokir
    [InlineData("tanpa_tagihan", 5)]
    [InlineData("belum", null)]     // calon manual (bukan PSB online): aturan tagihan tidak berlaku
    public async Task Terima_berhasil_bila_lunas_tidak_ada_kabar_atau_bukan_calon_online(string? status, int? psbId)
    {
        var (calonId, kelasId) = await SiapkanAsync(status, psbId);
        using var db = Buka();
        var hasil = await new PsbService(db).TerimaAsync(calonId, null, kelasId);
        Assert.True(hasil.Success, hasil.Message);
        Assert.Equal(1, await db.Siswa.CountAsync());
    }

    // ---------- penempatan acak ----------
    [Fact]
    public void Penempatan_merata_berimbang_kelamin_dan_dapat_diulang()
    {
        var calon = Enumerable.Range(1, 30).Select(i => new PenempatanKelas.Calon(i, i % 2 == 0)).ToList(); // 15 L + 15 P
        var kelas = new List<PenempatanKelas.Kelas> { new(1, 0, 0), new(2, 0, 0), new(3, 0, 0) };
        var a = PenempatanKelas.Susun(calon, kelas, 42);
        var b = PenempatanKelas.Susun(calon, kelas, 42);
        Assert.Equal(a, b);                                    // seed sama = hasil sama
        Assert.Equal(30, a.Select(x => x.CalonId).Distinct().Count()); // semua calon tepat satu kali
        foreach (var k in kelas)
        {
            var anggota = a.Where(x => x.KelasId == k.Id).Select(x => calon.First(c => c.Id == x.CalonId)).ToList();
            Assert.Equal(10, anggota.Count);
            Assert.Equal(5, anggota.Count(c => c.Laki));
        }
        Assert.NotEqual(a, PenempatanKelas.Susun(calon, kelas, 43)); // seed lain = pengacakan lain
    }

    [Fact]
    public void Penempatan_mengisi_kelas_yang_masih_longgar_lebih_dulu()
    {
        var calon = Enumerable.Range(1, 6).Select(i => new PenempatanKelas.Calon(i, i <= 3)).ToList();
        var kelas = new List<PenempatanKelas.Kelas> { new(1, 10, 5), new(2, 0, 0) };
        var hasil = PenempatanKelas.Susun(calon, kelas, 7);
        Assert.Equal(6, hasil.Count(x => x.KelasId == 2)); // kelas 1 sudah 10, kelas 2 kosong: ke-6 calon masuk kelas 2
    }

    [Fact]
    public void Penempatan_tanpa_calon_atau_tanpa_kelas_kosong()
    {
        Assert.Empty(PenempatanKelas.Susun([], [new(1, 0, 0)], 1));
        Assert.Empty(PenempatanKelas.Susun([new(1, true)], [], 1));
    }

    [Fact]
    public void Migrasi_berjalan_dan_model_sama_dengan_snapshot()
    {
        using var db = Buka();
        db.Database.Migrate();
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
