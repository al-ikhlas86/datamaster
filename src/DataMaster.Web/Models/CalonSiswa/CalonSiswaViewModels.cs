namespace DataMaster.Web.Models.CalonSiswa;

public class TingkatOption
{
    public required string Kode { get; set; }
    public required string Label { get; set; }
}

public class CalonSiswaIndexViewModel
{
    public string Status { get; set; } = "menunggu";
    public string? Tingkat { get; set; }
    public List<TingkatOption> TingkatOptions { get; set; } = [];
    public List<CalonSiswaRow> Rows { get; set; } = [];
}

public class CalonSiswaRow
{
    public int CalonSiswaId { get; set; }
    public required string Nama { get; set; }
    public required string JenisKelamin { get; set; }
    public string? TingkatDituju { get; set; }
    public string? AsalSekolah { get; set; }
    public string? NoHandphone { get; set; }
    public required string Status { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class CalonSiswaFormInput
{
    public int? CalonSiswaId { get; set; }
    public string Nama { get; set; } = "";
    public string JenisKelamin { get; set; } = "";
    public string? Nisn { get; set; }
    public string? TempatLahir { get; set; }
    public DateOnly? TanggalLahir { get; set; }
    public string? AsalSekolah { get; set; }
    public string? TingkatDituju { get; set; }
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

public class CalonSiswaFormViewModel
{
    public CalonSiswaFormInput Input { get; set; } = new();
    public List<TingkatOption> TingkatOptions { get; set; } = [];
    public Dictionary<string, string> Errors { get; set; } = [];
    public string? DokumenKkLama { get; set; }
    public string? DokumenAktaLama { get; set; }
    public string? DokumenKiaLama { get; set; }
    public string? DokumenIjazahLama { get; set; }

    // Hanya terisi di halaman Detail (bukan Create)
    public bool IsEdit => Input.CalonSiswaId.HasValue;
    public string Status { get; set; } = "menunggu";
    public string? Catatan { get; set; }
    public int? SiswaIdHasilTerima { get; set; }
    public DateTime? CreatedAt { get; set; }
    public List<Siswa.KelasOption> KelasOptionsUntukTerima { get; set; } = [];
}
