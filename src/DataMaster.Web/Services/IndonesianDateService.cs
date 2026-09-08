using System.Globalization;
using System.Text.RegularExpressions;

namespace DataMaster.Web.Services;

public record TanggalPeriode(DateOnly Mulai, DateOnly? Selesai);

// Port 1:1 dari app/Helpers/date_helper.php (parse_indonesian_date,
// parse_indonesian_date_range, indonesian_month_number, indonesian_date).
//
// BUG NYATA yang jadi alasan seluruh desain kelas ini (2026-08-26, lihat
// 01-siswa-psb.md §4 dan 03-akademik-jadwal.md §7): .NET/PHP punya masalah yang
// SAMA - parser tanggal generik (DateTime.Parse / strtotime) yang diminta membaca
// nama bulan bisa salah tafsir string ambigu. PHP strtotime("7 Juni 2027") diam-diam
// menghasilkan 2026-06-07 (mundur 1 tahun, "2027" ditelan jadi jam 20:27) TANPA
// error - 4 dari 42 agenda resmi sekolah pernah masuk dgn tahun keliru gara-gara ini.
// ATURAN WAJIB: kalau string mengandung huruf, method ini TIDAK PERNAH memakai
// DateTime.Parse/TryParse bawaan .NET sama sekali - HARUS lewat regex eksplisit +
// peta bulan manual di bawah. String ambigu/tidak dikenal DITOLAK (null / list
// kosong), bukan ditebak.
public static class IndonesianDateService
{
    private static readonly Dictionary<string, int> BulanMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["januari"] = 1, ["jan"] = 1,
        ["februari"] = 2, ["pebruari"] = 2, ["feb"] = 2,
        ["maret"] = 3, ["mar"] = 3,
        ["april"] = 4, ["apr"] = 4,
        ["mei"] = 5, ["may"] = 5,
        ["juni"] = 6, ["jun"] = 6, ["june"] = 6,
        ["juli"] = 7, ["jul"] = 7, ["july"] = 7,
        ["agustus"] = 8, ["ags"] = 8, ["agt"] = 8, ["agu"] = 8, ["aug"] = 8, ["august"] = 8,
        ["september"] = 9, ["sept"] = 9, ["sep"] = 9,
        ["oktober"] = 10, ["okt"] = 10, ["oct"] = 10, ["october"] = 10,
        ["november"] = 11, ["nopember"] = 11, ["nov"] = 11,
        ["desember"] = 12, ["des"] = 12, ["dec"] = 12, ["december"] = 12,
    };

    public static int? IndonesianMonthNumber(string name)
    {
        var key = name.Trim();
        return BulanMap.TryGetValue(key, out var n) ? n : null;
    }

    private static bool ContainsLetter(string s) => Regex.IsMatch(s, "[A-Za-z]");

    /// <summary>Satu tanggal saja. Return null kalau kosong/tidak dikenali/ambigu - JANGAN pernah menebak.</summary>
    public static DateOnly? ParseIndonesianDate(string? date)
    {
        if (string.IsNullOrWhiteSpace(date)) return null;
        var s = date.Trim();

        // "DD Bulan YYYY" - mis. "14 Maret 2018"
        var m = Regex.Match(s, @"^(\d{1,2})\s+([A-Za-z]+)\s+(\d{4})$");
        if (m.Success)
        {
            var bulan = IndonesianMonthNumber(m.Groups[2].Value);
            if (bulan is null) return null; // nama bulan tak dikenal -> TOLAK, jangan lanjut ke fallback
            var hari = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var tahun = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            return TryCreateDate(tahun, bulan.Value, hari);
        }

        // Normalisasi slash -> strip
        s = s.Replace('/', '-');

        // "DD-MM-YYYY" (1-2 digit hari/bulan)
        m = Regex.Match(s, @"^(\d{1,2})-(\d{1,2})-(\d{4})$");
        if (m.Success)
        {
            var hari = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var bulan = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var tahun = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            return TryCreateDate(tahun, bulan, hari);
        }

        // Fallback murni-angka lain (mis. "2026-07-13") - TAPI kalau masih ada
        // huruf, JANGAN diteruskan ke parser tanggal umum (itu akar bug strtotime).
        if (ContainsLetter(s)) return null;

        return DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : null;
    }

    private static DateOnly? TryCreateDate(int tahun, int bulan, int hari)
    {
        if (bulan < 1 || bulan > 12) return null;
        if (hari < 1 || hari > DateTime.DaysInMonth(tahun, bulan)) return null;
        return new DateOnly(tahun, bulan, hari);
    }

    /// <summary>
    /// Bisa hasilkan 0, 1, atau 2 periode dari 1 string mentah (mis. Excel kalender
    /// akademik: "13-17 Juli 2026", "18 &amp; 25 Juli 2026", "21 Des - 02 Jan 2027").
    /// Lihat daftar lengkap pola & contoh yang SENGAJA DITOLAK di 03-akademik-jadwal.md §7.
    /// </summary>
    public static List<TanggalPeriode> ParseIndonesianDateRange(object? raw)
    {
        if (raw is null) return [];
        string s;
        if (raw is double excelSerial)
        {
            // Serial Excel (angka > 1) - epoch 1899-12-30, sama offset dgn PHP (25569 hari ke 1970-01-01)
            if (excelSerial > 1)
            {
                var epoch = new DateOnly(1899, 12, 30);
                return [new TanggalPeriode(epoch.AddDays((int)excelSerial), null)];
            }
            s = excelSerial.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            s = raw.ToString() ?? "";
        }

        if (string.IsNullOrWhiteSpace(s)) return [];

        // Buang isi dalam kurung, samakan varian dash jadi '-', rapatkan spasi.
        s = Regex.Replace(s, @"\([^)]*\)", "");
        s = s.Replace('–', '-').Replace('—', '-').Replace('−', '-');
        s = Regex.Replace(s, @"\s+", " ").Trim();

        // "DUA tanggal terpisah dgn &" - mis. "18 & 25 Juli 2026"
        var m = Regex.Match(s, @"^(\d{1,2})\s*[&]\s*(\d{1,2})\s+([A-Za-z]+)\s+(\d{4})$");
        if (m.Success)
        {
            var bulan = IndonesianMonthNumber(m.Groups[3].Value);
            if (bulan is null) return [];
            var tahun = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
            var t1 = TryCreateDate(tahun, bulan.Value, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            var t2 = TryCreateDate(tahun, bulan.Value, int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
            if (t1 is null || t2 is null) return [];
            return [new TanggalPeriode(t1.Value, null), new TanggalPeriode(t2.Value, null)];
        }

        // "rentang lintas bulan/tahun" - mis. "25 Mar-03 April 2027", "21 Des - 02 Jan 2027"
        m = Regex.Match(s, @"^(\d{1,2})\s+([A-Za-z]+)\s*-\s*(\d{1,2})\s+([A-Za-z]+)\s+(\d{4})$");
        if (m.Success)
        {
            var bulan1 = IndonesianMonthNumber(m.Groups[2].Value);
            var bulan2 = IndonesianMonthNumber(m.Groups[4].Value);
            if (bulan1 is null || bulan2 is null) return [];
            var tahunAkhir = int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture);
            // Tahun HANYA ditulis sekali di akhir. Lintas tahun (Des..Jan): tahun awal = tahun akhir - 1.
            var tahunAwal = bulan1.Value > bulan2.Value ? tahunAkhir - 1 : tahunAkhir;
            var mulai = TryCreateDate(tahunAwal, bulan1.Value, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            var selesai = TryCreateDate(tahunAkhir, bulan2.Value, int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
            if (mulai is null || selesai is null || selesai < mulai) return [];
            return [new TanggalPeriode(mulai.Value, selesai.Value)];
        }

        // "rentang 1 bulan 1 tahun" - mis. "13-17 Juli 2026"
        m = Regex.Match(s, @"^(\d{1,2})\s*-\s*(\d{1,2})\s+([A-Za-z]+)\s+(\d{4})$");
        if (m.Success)
        {
            var bulan = IndonesianMonthNumber(m.Groups[3].Value);
            if (bulan is null) return [];
            var tahun = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
            var mulai = TryCreateDate(tahun, bulan.Value, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            var selesai = TryCreateDate(tahun, bulan.Value, int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
            // awal>akhir di bulan sama = AMBIGU, ditolak (bukan ditebak sbg bulan sebelumnya)
            if (mulai is null || selesai is null || selesai < mulai) return [];
            return [new TanggalPeriode(mulai.Value, selesai.Value)];
        }

        // Fallback tanggal tunggal
        var tunggal = ParseIndonesianDate(s);
        return tunggal is null ? [] : [new TanggalPeriode(tunggal.Value, null)];
    }

    private static readonly string[] HariIndonesia = ["Minggu", "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu"];
    private static readonly string[] BulanIndonesia =
    [
        "", "Januari", "Februari", "Maret", "April", "Mei", "Juni",
        "Juli", "Agustus", "September", "Oktober", "November", "Desember",
    ];

    /// <summary>Format tampilan Indonesia. 'short'/'medium'(default)/'long'/'full'/'datetime'.</summary>
    public static string IndonesianDate(DateTime? date, string format = "medium")
    {
        if (date is null) return "-";
        var d = date.Value;
        var namaBulan = BulanIndonesia[d.Month];
        var namaHari = HariIndonesia[(int)d.DayOfWeek];
        return format switch
        {
            "short" => $"{d:dd} {namaBulan[..3]} {d:yyyy}",
            "long" => $"{namaHari}, {d:dd} {namaBulan} {d:yyyy}",
            "full" => $"{namaHari}, {d:dd} {namaBulan} {d:yyyy} {d:HH:mm} WIB",
            "datetime" => $"{d:dd} {namaBulan} {d:yyyy}, {d:HH:mm} WIB",
            _ => $"{d:dd} {namaBulan} {d:yyyy}",
        };
    }
}
