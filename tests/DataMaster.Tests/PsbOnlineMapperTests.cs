using System.Text.Json;
using DataMaster.Data;
using DataMaster.Data.Psb;
using Xunit;

namespace DataMaster.Tests;

public class PsbOnlineMapperTests
{
    private static readonly IReadOnlyDictionary<string, string> Tingkat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["TK A"] = "TKA", ["Kelas 1"] = "1" };

    private static JsonElement Json(string teks) => JsonDocument.Parse(teks).RootElement.Clone();

    [Fact]
    public void Snapshot_lengkap_dipetakan_ke_kolom_calon_siswa()
    {
        var h = PsbOnlineMapper.Baca(Json("""
            {"psb_id":7,"nomor":"PSB-3-0007","status":"baru","gelombang":"Gelombang 1","nama":"  Ahmad  ","jenis_kelamin":"L","tempat_lahir":"Bandung","tanggal_lahir":"2020-03-05",
             "asal_sekolah":"TK Pelita","tingkat_dituju":"Kelas 1","alamat_jalan":"Jl. Mawar","nama_ayah":"Budi","pekerjaan_ayah":"Guru","nama_ibu":"Siti","no_hp":"+62 812-3456-7890"}
            """), Tingkat);
        Assert.NotNull(h);
        Assert.Equal((7, "PSB-3-0007", "baru", "Ahmad", (JenisKelamin?)JenisKelamin.L), (h!.PsbId, h.Nomor, h.Status, h.Nama, h.JenisKelamin));
        Assert.Equal(new DateOnly(2020, 3, 5), h.TanggalLahir);
        Assert.Equal("1", h.TingkatDituju);
        Assert.Equal("TK Pelita", h.AsalSekolah);
        Assert.Equal("Guru", h.PekerjaanAyah);
        Assert.Equal("6281234567890", h.NoHandphone);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"psb_id":0,"nomor":"x","nama":"y"}""")]
    [InlineData("""{"psb_id":5,"nomor":"PSB-1-0005"}""")]
    [InlineData("""{"psb_id":5,"nama":"Tanpa nomor"}""")]
    [InlineData("""{"psb_id":"5","nomor":"PSB-1-0005","nama":"Id bukan angka"}""")]
    [InlineData("[]")]
    public void Snapshot_tanpa_id_nama_atau_nomor_sah_ditolak(string json) => Assert.Null(PsbOnlineMapper.Baca(Json(json), Tingkat));

    [Fact]
    public void Nilai_buruk_menjadi_null_bukan_galat()
    {
        var h = PsbOnlineMapper.Baca(Json("""{"psb_id":9,"nomor":"PSB-1-0009","nama":"Citra","jenis_kelamin":"X","tanggal_lahir":"05-03-2020","no_hp":"123","tingkat_dituju":"Kelas 12"}"""), Tingkat)!;
        Assert.Null(h.JenisKelamin);
        Assert.Null(h.TanggalLahir);
        Assert.Null(h.NoHandphone);
        Assert.Equal("12", h.TingkatDituju);
    }

    [Theory]
    [InlineData("TK A", "TKA")]
    [InlineData("kelas 1", "1")]
    [InlineData("Kelas 3", "3")]
    [InlineData("Lainnya", "Lainnya")]
    public void Tingkat_dipetakan_lewat_nama_lalu_angka_lalu_apa_adanya(string masuk, string keluar)
    {
        var h = PsbOnlineMapper.Baca(Json($"{{\"psb_id\":1,\"nomor\":\"PSB-1-0001\",\"nama\":\"A\",\"tingkat_dituju\":\"{masuk}\"}}"), Tingkat)!;
        Assert.Equal(keluar, h.TingkatDituju);
    }

    [Fact]
    public void Teks_dipotong_dan_karakter_kontrol_dibuang()
    {
        var panjang = new string('a', 500);
        var h = PsbOnlineMapper.Baca(Json($"{{\"psb_id\":1,\"nomor\":\"PSB-1-0001\",\"nama\":\"Ab\\u0000c\\nd{panjang}\"}}"), Tingkat)!;
        Assert.Equal(100, h.Nama.Length);
        Assert.True(h.Nama.All(ch => !char.IsControl(ch)));
    }

    [Fact]
    public void Daftar_nama_berkas_dibaca_dari_json_tersimpan_dan_aman_bila_rusak()
    {
        var json = """{"berkas":[{"kunci":"dok_kk","nama":"kk.png","tipe":"webp","ukuran":10},{"kunci":"x","nama":"akta.pdf","tipe":"pdf"},{"kunci":"y"}]}""";
        Assert.Equal(new[] { "kk.png (webp)", "akta.pdf (pdf)" }, PsbOnlineMapper.NamaBerkas(json));
        Assert.Empty(PsbOnlineMapper.NamaBerkas(null));
        Assert.Empty(PsbOnlineMapper.NamaBerkas("{rusak"));
        Assert.Empty(PsbOnlineMapper.NamaBerkas("{}"));
    }

    [Fact]
    public void Json_terlalu_besar_tidak_disimpan_dan_label_status_dikenal()
    {
        Assert.NotNull(PsbOnlineMapper.JsonUntukSimpan(Json("{\"a\":1}")));
        Assert.Null(PsbOnlineMapper.JsonUntukSimpan(Json($"{{\"a\":\"{new string('x', 70000)}\"}}")));
        Assert.Equal("Diterima (lulus tes)", PsbOnlineMapper.LabelStatus("diterima"));
        Assert.Equal("-", PsbOnlineMapper.LabelStatus(null));
        Assert.Equal("Dihapus di web", PsbOnlineMapper.LabelStatus("dihapus"));
    }
}
