namespace DataMaster.Web.Models.Guru;

public class GuruIndexViewModel
{
    public string Keyword { get; set; } = "";
    public int PerPage { get; set; } = 50;
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int Total { get; set; }
    public int TotalGuruKelas { get; set; }
    public int TotalGuruBidang { get; set; }
    public int TotalKaryawan { get; set; }
    public List<GuruRow> Rows { get; set; } = [];
    public List<string> ImportErrors { get; set; } = [];
}

public class GuruRow
{
    public int GuruId { get; set; }
    public required string Nama { get; set; }
    public string? Nip { get; set; }
    public string? JenisKelamin { get; set; }
    public required string Jabatan { get; set; }
    public string? NoHandphone { get; set; }
    public string? NamaKelasWali { get; set; }
}

public class GuruFormInput
{
    public int? GuruId { get; set; }
    public string Nama { get; set; } = "";
    public string? Nip { get; set; }
    public string? JenisKelamin { get; set; }
    // Nilai form HANYA "guru"/"karyawan" - BUKAN nilai DB asli (guru_kelas/guru_bidang/karyawan).
    public string Jabatan { get; set; } = "guru";
    public string? MataPelajaran { get; set; }
    public string? GelarTerakhir { get; set; }
    public string? NoHandphone { get; set; }
    public string? Alamat { get; set; }
    // TIDAK diberi default 'true' SENGAJA - checkbox HTML tidak mengirim field
    // sama sekali saat dikosongkan (unchecked, tidak ada hidden fallback input di
    // _Form.cshtml), jadi model binder TIDAK PERNAH menyentuh properti ini kalau
    // checkbox unchecked. Default 'true' di sini dulu BUG NYATA: uncheck "Aktif"
    // lalu submit form edit tidak pernah menonaktifkan guru (nilai class-level
    // 'true' selalu menang krn field yg hilang tidak di-bind ulang) - ditemukan
    // testing 2026-09-08. Default bool (false) di sini justru BENAR: field hadir
    // (dicentang) -> true; field hilang (tidak dicentang) -> false. Sama persis
    // dgn PHP: getPost('status_aktif') !== null ? 1 : 0 (Guru.php update()).
    public bool StatusAktif { get; set; }
}

public class GuruFormViewModel
{
    public GuruFormInput Input { get; set; } = new();
    public Dictionary<string, string> Errors { get; set; } = [];
    public bool IsEdit => Input.GuruId.HasValue;
    public string? NamaKelasWali { get; set; }
    public string JabatanDbSaatIni { get; set; } = "guru_bidang";
    public DateTime? CreatedAt { get; set; }
}

public class GuruPrintRow
{
    public required string Nama { get; set; }
    public string? Nip { get; set; }
    public string? JenisKelamin { get; set; }
    public required string Jabatan { get; set; }
    public string? NamaKelasWali { get; set; }
    public string? MataPelajaran { get; set; }
    public string? NoHandphone { get; set; }
    public string? Alamat { get; set; }
}

// --- Import upsert 3-langkah (pola sama Siswa, field beda) ---

public class GuruImportDiffField
{
    public required string Field { get; set; }
    public required string Label { get; set; }
    public string? Lama { get; set; }
    public string? Baru { get; set; }
}

public class GuruImportPreviewRow
{
    public int RowNumber { get; set; }
    public required string NoHandphone { get; set; }
    public required string Nama { get; set; }
    public required string Type { get; set; } // "insert" / "update"
    public int? ExistingGuruId { get; set; }
    public List<GuruImportDiffField> Diff { get; set; } = [];
    public Dictionary<string, string?> Data { get; set; } = [];
}

public class GuruImportPreviewSession
{
    public List<GuruImportPreviewRow> Rows { get; set; } = [];
    public int InsertCount { get; set; }
    public int UpdateCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = [];
}

public static class GuruImportFieldLabels
{
    public static readonly Dictionary<string, string> Map = new()
    {
        ["nama"] = "Nama",
        ["jenis_kelamin"] = "Jenis Kelamin",
        ["jabatan"] = "Jabatan",
        ["gelar_terakhir"] = "Gelar Terakhir",
    };
}
