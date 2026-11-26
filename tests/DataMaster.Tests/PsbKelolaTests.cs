using DataMaster.Data.Psb;
using Xunit;

namespace DataMaster.Tests;

// Bagian murni "Kelola PSB dari Data Master": turunan alamat webview, pembersihan nama, pembacaan tiket, penyusunan URL.
public class PsbKelolaTests
{
    private static readonly string Tiket = new('a', 64);

    [Theory]
    [InlineData("https://app.yayasanalikhlas86.sch.id/hub-api", "https://app.yayasanalikhlas86.sch.id")]
    [InlineData("https://app.yayasanalikhlas86.sch.id/hub-api/", "https://app.yayasanalikhlas86.sch.id")]
    [InlineData("  https://alikhlas86.duckdns.org/hub-api  ", "https://alikhlas86.duckdns.org")]
    [InlineData("http://192.168.1.5:8080", "http://192.168.1.5:8080")]
    public void AlamatWeb_DariAlamatHub(string hub, string diharapkan) => Assert.Equal(diharapkan, PsbKelola.AlamatWeb(hub));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bukan url")]
    [InlineData("ftp://host/hub-api")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:sandi@host/hub-api")]
    [InlineData("https://host/hub-api?x=1")]
    public void AlamatWeb_TidakSahDitolak(string? hub) => Assert.Null(PsbKelola.AlamatWeb(hub));

    [Fact]
    public void BersihkanNama_BuangKontrolDanBatasi()
    {
        Assert.Equal("Bu Sari", PsbKelola.BersihkanNama("Bu\nSari"));
        Assert.Null(PsbKelola.BersihkanNama("  \t "));
        Assert.Null(PsbKelola.BersihkanNama(null));
        Assert.Equal(80, PsbKelola.BersihkanNama(new string('x', 300))!.Length);
    }

    [Fact]
    public void BacaTiket_HanyaBentukSah()
    {
        Assert.Equal(Tiket, PsbKelola.BacaTiket($"{{\"success\":true,\"data\":{{\"tiket\":\"{Tiket}\",\"berlaku_detik\":120}}}}"));
        Assert.Null(PsbKelola.BacaTiket($"{{\"success\":false,\"data\":{{\"tiket\":\"{Tiket}\"}}}}"));
        Assert.Null(PsbKelola.BacaTiket("{\"success\":true,\"data\":{\"tiket\":\"pendek\"}}"));
        Assert.Null(PsbKelola.BacaTiket($"{{\"success\":true,\"data\":{{\"tiket\":\"{new string('G', 64)}\"}}}}"));
        Assert.Null(PsbKelola.BacaTiket("{\"success\":true,\"data\":{\"tiket\":123}}"));
        Assert.Null(PsbKelola.BacaTiket("{\"success\":true}"));
        Assert.Null(PsbKelola.BacaTiket("<html>502</html>"));
        Assert.Null(PsbKelola.BacaTiket("[]"));
    }

    [Fact]
    public void UrlMasuk_TiketDiDalamQueryDanHanyaBilaSah()
    {
        Assert.Equal($"https://app.x.id/psb-dm?tiket={Tiket}", PsbKelola.UrlMasuk("https://app.x.id/", Tiket));
        Assert.Null(PsbKelola.UrlMasuk(null, Tiket));
        Assert.Null(PsbKelola.UrlMasuk("https://app.x.id", null));
        Assert.Null(PsbKelola.UrlMasuk("https://app.x.id", "a&b=c"));
    }
}
