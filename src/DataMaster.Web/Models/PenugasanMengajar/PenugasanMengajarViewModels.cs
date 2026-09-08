using DataMaster.Web.Models.CalonSiswa;

namespace DataMaster.Web.Models.PenugasanMengajar;

public class MapelOption
{
    public int MataPelajaranId { get; set; }
    public required string Nama { get; set; }
}

public class MapelTaut
{
    public int GuruMataPelajaranId { get; set; }
    public int MataPelajaranId { get; set; }
    public required string Nama { get; set; }
    public string? Tingkat { get; set; }
}

public class GuruDenganMapel
{
    public int GuruId { get; set; }
    public required string Nama { get; set; }
    public required string Jabatan { get; set; } // "guru_kelas" / "guru_bidang"
    public int? NomorUrut { get; set; }
    public List<MapelTaut> Mapel { get; set; } = [];
}

public class PenugasanMengajarIndexViewModel
{
    public List<GuruDenganMapel> GuruWithMapel { get; set; } = [];
    public List<MapelOption> MapelDropdown { get; set; } = [];
    public List<TingkatOption> TingkatOptions { get; set; } = [];
    public int NomorBerikut { get; set; }
}
