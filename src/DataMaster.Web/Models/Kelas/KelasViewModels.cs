namespace DataMaster.Web.Models.Kelas;

public class KelasRow
{
    public int KelasId { get; set; }
    public required string NamaKelas { get; set; }
    public required string Tingkat { get; set; }
    public int TotalSiswa { get; set; }
    public bool IsActive { get; set; }
}

public class TingkatGroup
{
    public required string Kode { get; set; }
    public required string Label { get; set; }
    public int TotalSiswa { get; set; }
    public List<KelasRow> KelasList { get; set; } = [];
}

public class TingkatRow
{
    public int TingkatId { get; set; }
    public required string Kode { get; set; }
    public required string Nama { get; set; }
    public int Urutan { get; set; }
    public bool IsActive { get; set; }
}

public class WaliKelasSlot
{
    public int GuruId { get; set; }
    public required string Nama { get; set; }
}

public class KelasIndexViewModel
{
    public List<TingkatGroup> PerTingkat { get; set; } = [];
    public int TotalKelas { get; set; }
    public int TotalSiswa { get; set; }
    public List<TingkatRow> SemuaTingkat { get; set; } = [];
    public Dictionary<int, List<WaliKelasSlot>> WaliPerKelas { get; set; } = [];
    public int TahunAjaranId { get; set; }
    public int TahunAjaranAktif { get; set; }
    public List<(int Id, string Nama)> TahunAjaranList { get; set; } = [];
}

public class KelasDetailViewModel
{
    public int KelasId { get; set; }
    public required string NamaKelas { get; set; }
    public required string Tingkat { get; set; }
    public List<SiswaRingkas> SiswaKelas { get; set; } = [];
    public List<SiswaRingkas> SiswaTanpaKelas { get; set; } = [];
}

public class SiswaRingkas
{
    public int SiswaId { get; set; }
    public required string Nama { get; set; }
    public required string Nis { get; set; }
    public required string JenisKelamin { get; set; }
}
