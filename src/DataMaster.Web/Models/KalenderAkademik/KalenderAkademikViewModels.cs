namespace DataMaster.Web.Models.KalenderAkademik;

public class AgendaRow
{
    public int KalenderAkademikId { get; set; }
    public required string Judul { get; set; }
    public string? Kategori { get; set; }
    public string? Warna { get; set; }
    public DateOnly TanggalMulai { get; set; }
    public DateOnly? TanggalSelesai { get; set; }
    public string? Waktu { get; set; }
    public string? Sasaran { get; set; }
    public bool IsLibur { get; set; }
    public string? Keterangan { get; set; }
}

public class KalenderIndexViewModel
{
    public int TahunAjaranId { get; set; }
    public List<(int Id, string Nama)> TahunAjaranList { get; set; } = [];
    public List<AgendaRow> Agenda { get; set; } = [];
    public List<string> KategoriList { get; set; } = [];
    public Dictionary<string, string> WarnaSaran { get; set; } = [];
}
