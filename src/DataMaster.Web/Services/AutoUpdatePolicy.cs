namespace DataMaster.Web.Services;

// Aturan murni (tanpa I/O) untuk pembaruan otomatis layanan Windows - dipisah dari
// ServiceAutoUpdateHostedService supaya mudah diperiksa/diuji.
public static class AutoUpdatePolicy
{
    // "v1.7.1" / "1.7.1" -> Version 1.7.1.0 (selalu 4 bagian, sama seperti versi assembly).
    public static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;
        var bersih = tag.Trim().TrimStart('v', 'V');
        var bagian = bersih.Split('.');
        if (bagian.Length is < 1 or > 4) return false;
        var padded = bagian.Concat(Enumerable.Repeat("0", 4 - bagian.Length));
        if (!Version.TryParse(string.Join('.', padded), out var hasil)) return false;
        version = hasil;
        return true;
    }

    // Jendela malam [jamMulai, jamSelesai) menurut jam lokal PC - mendukung lewat tengah malam
    // (22 -> 5). jamMulai == jamSelesai berarti SEPANJANG HARI.
    public static bool DalamJendela(DateTime waktuLokal, int jamMulai, int jamSelesai)
    {
        if (jamMulai == jamSelesai) return true;
        var jam = waktuLokal.Hour;
        return jamMulai < jamSelesai
            ? jam >= jamMulai && jam < jamSelesai
            : jam >= jamMulai || jam < jamSelesai;
    }

    // Pembaruan baru diterapkan di jendela malam (layanan restart ~1 menit, jangan saat jam sekolah).
    // Jika PC server tidak pernah menyala di jendela itu, pembaruan yang sudah menunggu lebih dari
    // batasPaksa tetap diterapkan kapan saja supaya tidak tertinggal selamanya.
    public static bool BolehTerapkan(DateTime sekarang, DateTime? tertundaSejak, TimeSpan batasPaksa, int jamMulai, int jamSelesai)
    {
        if (DalamJendela(sekarang, jamMulai, jamSelesai)) return true;
        return tertundaSejak is { } sejak && sekarang - sejak >= batasPaksa;
    }
}
