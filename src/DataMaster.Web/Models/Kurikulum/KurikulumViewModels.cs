namespace DataMaster.Web.Models.Kurikulum;

public class MapelRow
{
    public int MataPelajaranId { get; set; }
    public required string Nama { get; set; }
    public string? Kode { get; set; }
    public string? Kelompok { get; set; }
    public int Urutan { get; set; }
}

public class JamRow
{
    public int JamPelajaranId { get; set; }
    public int JamKe { get; set; }
    public TimeOnly JamMulai { get; set; }
    public TimeOnly JamSelesai { get; set; }
    public string Jenis { get; set; } = "pelajaran";
    public string? Label { get; set; }
}

public class TingkatOpt
{
    public required string Kode { get; set; }
    public required string Nama { get; set; }
}

public class KurikulumIndexViewModel
{
    public int TahunAjaranId { get; set; }
    public string Tab { get; set; } = "mapel";
    public List<(int Id, string Nama)> TahunAjaranList { get; set; } = [];
    public List<MapelRow> MapelList { get; set; } = [];
    public Dictionary<string, List<MapelRow>> MapelPerKelompok { get; set; } = [];
    public List<TingkatOpt> TingkatList { get; set; } = [];
    // matriks[tingkatKode][mataPelajaranId] = jp
    public Dictionary<string, Dictionary<int, int>> Matriks { get; set; } = [];
    // jamPerHari[hari] = list jam
    public Dictionary<string, List<JamRow>> JamPerHari { get; set; } = [];
    public string[] HariList { get; set; } = ["senin", "selasa", "rabu", "kamis", "jumat", "sabtu", "minggu"];
}
