namespace DataMaster.Web.Models.JadwalPelajaran;

public class KelasOpt
{
    public int KelasId { get; set; }
    public required string NamaKelas { get; set; }
    public required string Tingkat { get; set; }
}

public class GuruOpt
{
    public int GuruId { get; set; }
    public int? NomorUrut { get; set; }
    public required string Nama { get; set; }
}

public class MapelKodeOpt
{
    public int MataPelajaranId { get; set; }
    public string? Kode { get; set; }
    public required string Nama { get; set; }
}

// 1 sel grid: hari x jam_ke untuk 1 kelas.
public class GridSel
{
    public int? JamPelajaranId { get; set; } // null = jam ini tidak ada di hari ini (sel benar2 kosong, non-editable)
    public string Jenis { get; set; } = "pelajaran"; // pelajaran|kegiatan
    public string? Label { get; set; } // label kegiatan (kalau Jenis=kegiatan)
    public bool DibatasiTingkatLain { get; set; } // true = jam ada tapi batasan tingkat tidak termasuk kelas ini -> "-"
    public string? Kode { get; set; } // isi saat ini, mis. "35K" atau "F" (mapel tanpa guru tetap)
}

public class GridBaris
{
    public int JamKe { get; set; }
    public string? WaktuLabel { get; set; } // "07:30-08:10" dari jam manapun yg punya jam_ke ini
    public Dictionary<string, GridSel> PerHari { get; set; } = [];
}

public class ProgresMapelRow
{
    public int MataPelajaranId { get; set; }
    public required string Nama { get; set; }
    public int Alokasi { get; set; }
    public int Terpakai { get; set; }
}

public class JadwalPelajaranIndexViewModel
{
    public int TahunAjaranId { get; set; }
    public string Semester { get; set; } = "ganjil";
    public int? KelasId { get; set; }
    public List<(int Id, string Nama)> TahunAjaranList { get; set; } = [];
    public List<KelasOpt> KelasList { get; set; } = [];
    public bool AdaTahunAjaran { get; set; }
    public bool AdaJamBelajar { get; set; }
    public bool AdaKelasAktif { get; set; }
    public string[] HariAktif { get; set; } = [];
    public List<GridBaris> Grid { get; set; } = [];
    public List<ProgresMapelRow> Progres { get; set; } = [];
    public List<MapelKodeOpt> MapelList { get; set; } = [];
    public List<GuruOpt> GuruList { get; set; } = [];
    public Dictionary<string, List<int>> PiketPerHari { get; set; } = [];
}

public class PerGuruBebanRow
{
    public int GuruId { get; set; }
    public int? NomorUrut { get; set; }
    public required string Nama { get; set; }
    public int Jam { get; set; }
}

public class PerGuruJadwalRow
{
    public string Hari { get; set; } = "";
    public int JamKe { get; set; }
    public string WaktuLabel { get; set; } = "";
    public required string NamaKelas { get; set; }
    public required string NamaMapel { get; set; }
}

public class JadwalPerGuruViewModel
{
    public int TahunAjaranId { get; set; }
    public string Semester { get; set; } = "ganjil";
    public int? GuruId { get; set; }
    public List<(int Id, string Nama)> TahunAjaranList { get; set; } = [];
    public List<GuruOpt> GuruList { get; set; } = [];
    public List<PerGuruJadwalRow> Jadwal { get; set; } = [];
    public List<PerGuruBebanRow> Beban { get; set; } = [];
}

public class CetakKelasKolom
{
    public int KelasId { get; set; }
    public required string NamaKelas { get; set; }
}

public class CetakSel
{
    public string Jenis { get; set; } = "pelajaran";
    public string? Label { get; set; }
    public string? Kode { get; set; }
    public bool Kosong { get; set; } = true;
}

public class CetakBaris
{
    public int JamKe { get; set; }
    public string? WaktuLabel { get; set; }
    public bool BarisKegiatan { get; set; }
    public string? LabelKegiatan { get; set; }
    // key: kelasId (hanya dipakai kalau bukan baris kegiatan)
    public Dictionary<int, CetakSel> PerKelas { get; set; } = [];
}

public class JadwalCetakViewModel
{
    public required string TahunAjaranNama { get; set; }
    public string Semester { get; set; } = "ganjil";
    public string[] HariAktif { get; set; } = [];
    public List<CetakKelasKolom> KelasList { get; set; } = [];
    public Dictionary<string, List<CetakBaris>> PerHari { get; set; } = [];
    public Dictionary<string, List<(string Nama, int? NomorUrut, string GuruNama)>> PiketPerHari { get; set; } = [];
    public List<MapelKodeOpt> LegendMapel { get; set; } = [];
    public List<GuruOpt> LegendGuru { get; set; } = [];
}
