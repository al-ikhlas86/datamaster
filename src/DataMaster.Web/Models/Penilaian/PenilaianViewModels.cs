using DataMaster.Web.Models.Siswa;

namespace DataMaster.Web.Models.Penilaian;

public class SiswaSikapRow
{
    public int SiswaId { get; set; }
    public required string Nama { get; set; }
    public required string Nis { get; set; }
    public string? GradeGanjil { get; set; } // string enum GradeSikap, null = belum dinilai
    public string? GradeGenap { get; set; }
}

public class PenilaianSikapIndexViewModel
{
    public bool AdaTahunAktif { get; set; }
    public string? TahunAktifNama { get; set; }
    public int TahunAjaranId { get; set; }
    public int? KelasId { get; set; }
    public List<KelasOption> KelasList { get; set; } = [];
    public List<SiswaSikapRow> Siswa { get; set; } = [];
}
