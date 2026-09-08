using DataMaster.Web.Models.Kelas;

namespace DataMaster.Web.Models.Ekskul;

public class EkskulRow
{
    public int EkskulId { get; set; }
    public required string Nama { get; set; }
    public string? Pembina { get; set; }
    public string? Hari { get; set; }
    public int TotalPeserta { get; set; }
    public bool IsActive { get; set; }
}

public class EkskulIndexViewModel
{
    public List<EkskulRow> Rows { get; set; } = [];
    public int TotalAktif { get; set; }
    public int TotalPesertaSemua { get; set; }
}

public class EkskulPesertaRow
{
    public int SiswaId { get; set; }
    public required string Nama { get; set; }
    public required string Nis { get; set; }
    public string? NamaKelas { get; set; }
    public DateOnly TanggalDaftar { get; set; }
}

public class EkskulDetailViewModel
{
    public int EkskulId { get; set; }
    public required string Nama { get; set; }
    public string? Pembina { get; set; }
    public string? Hari { get; set; }
    public string? Deskripsi { get; set; }
    public List<EkskulPesertaRow> Peserta { get; set; } = [];
    public List<SiswaRingkas> SiswaBelumIkut { get; set; } = [];
}
