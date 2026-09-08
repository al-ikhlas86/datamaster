using DataMaster.Web.Models.Siswa;

namespace DataMaster.Web.Models.Akademik;

public class SiswaAktifRow
{
    public int SiswaId { get; set; }
    public required string Nama { get; set; }
    public required string Nis { get; set; }
    public required string JenisKelamin { get; set; }
}

public class KelompokKelasSiswa
{
    public int? KelasId { get; set; }
    public required string NamaKelas { get; set; } // "Tanpa Kelas" kalau KelasId null
    public required string Tingkat { get; set; }
    public List<SiswaAktifRow> SiswaList { get; set; } = [];
}

public class AkademikIndexViewModel
{
    public bool AdaTahunAktif { get; set; }
    public string? TahunAktifNama { get; set; }
    public List<KelompokKelasSiswa> KelompokSiswa { get; set; } = [];
    public List<KelasOption> KelasList { get; set; } = [];
}

public class RiwayatAkademikRow
{
    public int SiswaId { get; set; }
    public required string Nama { get; set; }
    public required string Nis { get; set; }
    public required string JenisKelamin { get; set; }
    public string? NamaKelas { get; set; }
    public required string Status { get; set; } // aktif/naik/lulus
    public string? KelasTujuanNama { get; set; } // "Naik ke {kelas}" kalau status=naik
}

public class AkademikArsipViewModel
{
    public int TahunAjaranId { get; set; }
    public int? KelasId { get; set; }
    public string? Status { get; set; }
    public List<(int Id, string Nama)> TahunAjaranList { get; set; } = [];
    public List<KelasOption> KelasList { get; set; } = [];
    public List<RiwayatAkademikRow> Riwayat { get; set; } = [];
}

public class RekapLulusanRow
{
    public int SiswaId { get; set; }
    public required string Nama { get; set; }
    public required string Nis { get; set; }
    public required string JenisKelamin { get; set; }
    public string? KelasTerakhir { get; set; }
    public string? AsalSekolah { get; set; }
}

public class RekapLulusanViewModel
{
    public int TahunAjaranId { get; set; }
    public List<(int Id, string Nama)> TahunAjaranList { get; set; } = [];
    public List<RekapLulusanRow> Lulusan { get; set; } = [];
}
