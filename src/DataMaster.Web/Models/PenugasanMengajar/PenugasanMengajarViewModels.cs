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

    // KelasMengajar (2026-09-24, diminta user - "misal ada 2 guru IPA tingkat
    // kelas 5, kan kelas 5 ada berbagai kelas, jadinya kan jelas") - Guru
    // Pengampu SENDIRI cuma nyimpen Guru+MataPelajaran+Tingkat (TIDAK py kelas
    // spesifik sama sekali, lihat catatan Entities/GuruMataPelajaran.cs) -
    // daftar kelas KONKRET di sini diturunkan LANGSUNG dari Jadwal Pelajaran
    // tahun ajaran aktif (sumber kebenaran SEBENARNYA soal "siapa ngajar
    // kelas mana", lihat JadwalPelajaranController), BUKAN kolom baru yang
    // bisa menyimpang dari jadwal sungguhan. Kosong = guru terdaftar
    // "berhak" ngajar mapel ini tapi BELUM ada slot jadwal nyata utk itu.
    public List<string> KelasMengajar { get; set; } = [];
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
