namespace DataMaster.Web.Models.Siswa;

// Konstanta regex PERSIS dari app/Controllers/BaseController.php - dipakai lintas
// modul PHP asli (Siswa, CalonSiswa, Guru). .NET Regex mendukung \p{L}/\p{N}
// langsung tanpa perlu flag /u tambahan.
public static class ValidationPatterns
{
    public const string RegexNama = @"^[\p{L}\s'.,\-]+$";
    public const string RegexTeksPendek = @"^[\p{L}\p{N}\s'.,/\-]+$";
    public const string PesanRegexNama = "hanya boleh berisi huruf, spasi, titik, koma, strip, dan tanda petik satu.";
    public const string PesanRegexTeksPendek = "mengandung karakter yang tidak diizinkan.";
}

public class SiswaIndexViewModel
{
    public string Keyword { get; set; } = "";
    public int PerPage { get; set; } = 50;
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalSiswa { get; set; }
    public int TotalL { get; set; }
    public int TotalP { get; set; }
    public List<SiswaRow> Rows { get; set; } = [];
    public List<string> ImportErrors { get; set; } = [];
}

public class SiswaRow
{
    public int SiswaId { get; set; }
    public required string Nama { get; set; }
    public required string Nis { get; set; }
    public string? Nisn { get; set; }
    public required string JenisKelamin { get; set; }
    public string? NamaKelas { get; set; }
    public string? TempatLahir { get; set; }
    public DateOnly? TanggalLahir { get; set; }
    public string Status { get; set; } = "aktif";
}

public class KelasOption
{
    public int KelasId { get; set; }
    public required string NamaKelas { get; set; }
}

public class SiswaFormInput
{
    public int? SiswaId { get; set; }
    public string Nama { get; set; } = "";
    public string JenisKelamin { get; set; } = "";
    public string Nis { get; set; } = "";
    public string? Nisn { get; set; }
    public int? KelasId { get; set; }
    public string? TempatLahir { get; set; }
    public DateOnly? TanggalLahir { get; set; }
    public string? AsalSekolah { get; set; }
    public string? AlamatJalan { get; set; }
    public string? AlamatRt { get; set; }
    public string? AlamatRw { get; set; }
    public string? AlamatKelurahan { get; set; }
    public string? AlamatKecamatan { get; set; }
    public string? NamaAyah { get; set; }
    public string? PekerjaanAyah { get; set; }
    public string? NamaIbu { get; set; }
    public string? PekerjaanIbu { get; set; }
    public string? NoHandphone { get; set; }
    public IFormFile? DokumenKk { get; set; }
    public IFormFile? DokumenAkta { get; set; }
    public IFormFile? DokumenKia { get; set; }
    public IFormFile? DokumenIjazah { get; set; }
}

public class SiswaFormViewModel
{
    public SiswaFormInput Input { get; set; } = new();
    public List<KelasOption> KelasOptions { get; set; } = [];
    public Dictionary<string, string> Errors { get; set; } = [];
    // Nama file dokumen yang SUDAH tersimpan (mode edit) - dipakai render preview/link "Lihat".
    public string? DokumenKkLama { get; set; }
    public string? DokumenAktaLama { get; set; }
    public string? DokumenKiaLama { get; set; }
    public string? DokumenIjazahLama { get; set; }
    public DateTime? CreatedAt { get; set; }
    public bool IsEdit => SiswaId.HasValue;
    public int? SiswaId => Input.SiswaId;
}

public class SiswaPrintRow
{
    public required string Nis { get; set; }
    public required string Nama { get; set; }
    public required string JenisKelamin { get; set; }
    public string? NamaKelas { get; set; }
    public string? TempatLahir { get; set; }
    public DateOnly? TanggalLahir { get; set; }
    public string? AsalSekolah { get; set; }
    public string Alamat { get; set; } = "";
    public string? NamaAyah { get; set; }
    public string? NamaIbu { get; set; }
    public string? NoHandphone { get; set; }
}

// --- Alur Import Upsert 3-langkah (preview session -> apply), lihat 01-siswa-psb.md §3.13-15 ---

public class ImportDiffField
{
    public required string Field { get; set; }
    public required string Label { get; set; }
    public string? Lama { get; set; }
    public string? Baru { get; set; }
}

public class ImportPreviewRow
{
    public int RowNumber { get; set; }
    public required string Nis { get; set; }
    public required string Nama { get; set; }
    // "insert" atau "update"
    public required string Type { get; set; }
    public int? ExistingSiswaId { get; set; }
    public List<ImportDiffField> Diff { get; set; } = [];
    public Dictionary<string, string?> Data { get; set; } = [];
}

public class ImportPreviewSession
{
    public List<ImportPreviewRow> Rows { get; set; } = [];
    public int InsertCount { get; set; }
    public int UpdateCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = [];
    public int? KelasId { get; set; }
    public string? NamaKelas { get; set; }
}

public static class ImportFieldLabels
{
    public static readonly Dictionary<string, string> Map = new()
    {
        ["nama"] = "Nama",
        ["jenis_kelamin"] = "Jenis Kelamin",
        ["nisn"] = "NISN",
        ["tempat_lahir"] = "Tempat Lahir",
        ["tanggal_lahir"] = "Tanggal Lahir",
        ["asal_sekolah"] = "Asal TK",
        ["alamat_jalan"] = "Jalan",
        ["alamat_rt"] = "RT",
        ["alamat_rw"] = "RW",
        ["alamat_kelurahan"] = "Kelurahan",
        ["alamat_kecamatan"] = "Kecamatan",
        ["nama_ayah"] = "Nama Ayah",
        ["pekerjaan_ayah"] = "Pekerjaan Ayah",
        ["nama_ibu"] = "Nama Ibu",
        ["pekerjaan_ibu"] = "Pekerjaan Ibu",
        ["no_handphone"] = "No HP",
    };
}
