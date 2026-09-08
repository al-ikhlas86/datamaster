namespace DataMaster.Web.Models.Dashboard;

public class SiswaTerbaruRow
{
    public int SiswaId { get; set; }
    public required string Nis { get; set; }
    public required string Nama { get; set; }
    public required string JenisKelamin { get; set; }
    public string? AsalSekolah { get; set; }
}

public class PegawaiTerbaruRow
{
    public int GuruId { get; set; }
    public required string Nama { get; set; }
    public required string JenisKelamin { get; set; }
    public string? NoHandphone { get; set; }
}

public class LangkahPersiapan
{
    public required string Nama { get; set; }
    public bool Ok { get; set; }
    public required string Url { get; set; }
    public required string Ket { get; set; }
}

public class DashboardPendidikanViewModel
{
    public int TotalSiswa { get; set; }
    public int TotalL { get; set; }
    public int TotalP { get; set; }
    public int TotalAlumni { get; set; }
    public List<SiswaTerbaruRow> RecentSiswa { get; set; } = [];
    public string? TahunAktifNama { get; set; }
    public Dictionary<string, int> PerTingkat { get; set; } = [];
    public List<LangkahPersiapan> LangkahPersiapan { get; set; } = [];
    public bool SemuaLangkahSelesai => LangkahPersiapan.Count > 0 && LangkahPersiapan.All(l => l.Ok);
}

public class DashboardPerusahaanViewModel
{
    public int TotalPegawai { get; set; }
    public int TotalL { get; set; }
    public int TotalP { get; set; }
    public List<PegawaiTerbaruRow> RecentPegawai { get; set; } = [];
}
