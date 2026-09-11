using System.Security.Cryptography;
using System.Text;

namespace DataMaster.Web.Services;

// Kode Pemulihan (2026-09-11, poin #5) - lihat catatan lengkap di User.cs
// kenapa ini BUKAN OTP asli (app desktop mandiri, tidak ada infrastruktur
// pengiriman SMS/WA/email). Kode SELALU 16 karakter alfanumerik huruf besar
// (dikelompokkan 4-4-4-4 utk gampang dibaca/ditulis tangan), dibangkitkan
// pakai RandomNumberGenerator (CSPRNG, bukan Random biasa) - entropi ~83 bit,
// jauh lebih dari cukup utk sesuatu yang TIDAK PERNAH ditebak lewat brute
// force online (tidak ada endpoint yang menerima percobaan tak terbatas -
// lihat LoginThrottleService yg juga dipakai di jalur reset).
public static class RecoveryCodeService
{
    private const string Alfabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // tanpa 0/O/1/I - hindari salah baca tulis tangan

    public static string Generate()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < 16; i++)
        {
            if (i > 0 && i % 4 == 0) sb.Append('-');
            sb.Append(Alfabet[RandomNumberGenerator.GetInt32(Alfabet.Length)]);
        }
        return sb.ToString();
    }

    public static string Hash(string kode) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NormalisasiUntukBandingkan(kode)))).ToLowerInvariant();

    public static bool Cocok(string kodeInput, string? hashTersimpan)
    {
        if (string.IsNullOrEmpty(hashTersimpan)) return false;
        var hashInput = Hash(kodeInput);
        // Perbandingan waktu-konstan - hindari timing attack membedakan "hampir cocok" vs "sama sekali salah".
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hashInput), Encoding.UTF8.GetBytes(hashTersimpan));
    }

    // Toleran user ketik tanpa strip/beda kapitalisasi - tetap dibandingkan
    // sbg kode yang sama (kode aslinya SELALU huruf besar, - sebagai pemisah).
    private static string NormalisasiUntukBandingkan(string kode) =>
        kode.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");
}
