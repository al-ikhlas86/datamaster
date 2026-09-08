namespace DataMaster.Data.Entities;

// jam_pelajaran - hari TANPA baris = otomatis dianggap LIBUR (mendukung sekolah
// 5 atau 6 hari tanpa konfigurasi tambahan). Unique (TahunAjaranId,Hari,JamKe).
// Dua aturan bentrok (nomor jam & tumpang-tindih waktu) HARUS diterapkan identik di
// jalur manual (form tambah) MAUPUN import Excel - lihat 03-akademik-jadwal.md §3
// (storeJam/processImportJam) dan §10 poin D untuk PERINGATAN inkonsistensi nyata:
// updateJamBatch() di PHP asli TIDAK mengecek bentrok sama sekali - replikasikan
// celah itu apa adanya kecuali user minta diperbaiki secara eksplisit.
public class JamPelajaran
{
    public int JamPelajaranId { get; set; }
    public int TahunAjaranId { get; set; }
    public Hari Hari { get; set; }
    public int JamKe { get; set; }
    public TimeOnly JamMulai { get; set; }
    public TimeOnly JamSelesai { get; set; }
    public JenisJamPelajaran Jenis { get; set; } = JenisJamPelajaran.pelajaran;
    public string? Label { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public TahunAjaran TahunAjaran { get; set; } = null!;
    public ICollection<JamPelajaranTingkat> BatasanTingkat { get; set; } = new List<JamPelajaranTingkat>();
    public ICollection<JadwalPelajaran> JadwalPelajaranList { get; set; } = new List<JadwalPelajaran>();
}
