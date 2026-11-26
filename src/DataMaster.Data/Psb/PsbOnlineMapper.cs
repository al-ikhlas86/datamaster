using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json;
using DataMaster.Data.Entities;

namespace DataMaster.Data.Psb;

// Pemetaan snapshot pendaftar PSB online (diterima dari Hub API) ke calon siswa Data Master (2026-11-27). Murni: tanpa database, tanpa jaringan.
// Semua nilai dipotong ke panjang wajar dan dibersihkan; field yang tidak dikenal/rusak menjadi null (bukan galat) supaya satu snapshot buruk
// tidak menghentikan sinkron pendaftar lain.
public static partial class PsbOnlineMapper
{
    public sealed record Hasil(
        int PsbId, string Nomor, string Status, string? Gelombang, string Nama, JenisKelamin? JenisKelamin, string? Nisn, string? TempatLahir,
        DateOnly? TanggalLahir, string? AsalSekolah, string? TingkatDituju, string? AlamatJalan, string? AlamatRt, string? AlamatRw, string? AlamatKelurahan,
        string? AlamatKecamatan, string? NamaAyah, string? PekerjaanAyah, string? NamaIbu, string? PekerjaanIbu, string? NoHandphone);

    private const int MaksJson = 60000;

    /// <summary>Null bila snapshot tidak punya id/nama/nomor yang sah.</summary>
    public static Hasil? Baca(JsonElement p, IReadOnlyDictionary<string, string> tingkatNamaKeKode)
    {
        if (p.ValueKind != JsonValueKind.Object) return null;
        var psbId = Int(p, "psb_id");
        var nama = Str(p, "nama", 100);
        var nomor = Str(p, "nomor", 30);
        if (psbId is not > 0 || nama is null || nomor is null) return null;
        return new Hasil(
            psbId.Value, nomor, Str(p, "status", 20) ?? "baru", Str(p, "gelombang", 120), nama, KodeJenisKelamin(Str(p, "jenis_kelamin", 2)), Str(p, "nisn", 20),
            Str(p, "tempat_lahir", 100), Tanggal(Str(p, "tanggal_lahir", 10)), Str(p, "asal_sekolah", 150), Tingkat(Str(p, "tingkat_dituju", 20), tingkatNamaKeKode),
            Str(p, "alamat_jalan", 255), Str(p, "alamat_rt", 10), Str(p, "alamat_rw", 10), Str(p, "alamat_kelurahan", 100), Str(p, "alamat_kecamatan", 100),
            Str(p, "nama_ayah", 100), Str(p, "pekerjaan_ayah", 100), Str(p, "nama_ibu", 100), Str(p, "pekerjaan_ibu", 100), Digit(Str(p, "no_hp", 25)));
    }

    /// <summary>Teks mentah snapshot untuk disimpan (dibatasi); berlebih -> null supaya tidak membengkakkan database.</summary>
    public static string? JsonUntukSimpan(JsonElement p)
    {
        var teks = p.GetRawText();
        return teks.Length <= MaksJson ? teks : null;
    }

    /// <summary>Daftar nama berkas dari snapshot tersimpan (untuk ditampilkan), kosong bila tidak ada/rusak.</summary>
    public static List<string> NamaBerkas(string? json)
    {
        var hasil = new List<string>();
        if (string.IsNullOrWhiteSpace(json)) return hasil;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("berkas", out var arr) || arr.ValueKind != JsonValueKind.Array) return hasil;
            foreach (var b in arr.EnumerateArray())
            {
                var n = Str(b, "nama", 200);
                var tipe = Str(b, "tipe", 10);
                if (n is not null) hasil.Add(tipe is null ? n : $"{n} ({tipe})");
            }
        }
        catch (JsonException) { }
        return hasil;
    }

    public static string LabelStatus(string? status) => status switch
    {
        "baru" => "Formulir diterima",
        "diverifikasi" => "Diverifikasi",
        "diterima" => "Diterima (lulus tes)",
        "cadangan" => "Cadangan",
        "ditolak" => "Ditolak",
        "mundur" => "Mundur",
        _ => status ?? "-",
    };

    private static string? Str(JsonElement o, string nama, int maks)
    {
        if (o.ValueKind != JsonValueKind.Object || !o.TryGetProperty(nama, out var v) || v.ValueKind != JsonValueKind.String) return null;
        var s = new string((v.GetString() ?? "").Where(c => !char.IsControl(c) || c == ' ').ToArray()).Trim();
        if (s.Length == 0) return null;
        return s.Length > maks ? s[..maks] : s;
    }

    private static int? Int(JsonElement o, string nama) => o.TryGetProperty(nama, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    private static JenisKelamin? KodeJenisKelamin(string? s) => s switch { "L" => JenisKelamin.L, "P" => JenisKelamin.P, _ => null };

    private static DateOnly? Tanggal(string? s) => s is not null && DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static string? Digit(string? s)
    {
        if (s is null) return null;
        var d = new string(s.Where(char.IsAsciiDigit).ToArray());
        return d.Length is >= 6 and <= 20 ? d : null;
    }

    /// <summary>"Kelas 1" -> kode tingkat Data Master lewat nama tingkat; bila tak ada, angka dari "Kelas N"; selain itu teks apa adanya.</summary>
    private static string? Tingkat(string? s, IReadOnlyDictionary<string, string> namaKeKode)
    {
        if (s is null) return null;
        if (namaKeKode.TryGetValue(s, out var kode)) return kode;
        var cocok = KelasAngka().Match(s);
        return cocok.Success ? cocok.Groups[1].Value : s;
    }

    [GeneratedRegex(@"^Kelas\s+(\d{1,2})$", RegexOptions.IgnoreCase)]
    private static partial Regex KelasAngka();
}
