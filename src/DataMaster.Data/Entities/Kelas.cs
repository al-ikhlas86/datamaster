namespace DataMaster.Data.Entities;

// kelas - "Tingkat" disimpan sbg string bebas (kode tingkat), bukan FK ke Tingkat.
// Nama_kelas sudah termasuk prefix tingkat (dibangun via formatNamaKelas(), lihat
// Kelas::formatNamaKelas di 02-guru-kelas-struktur.md §7.13) - jangan dipisah lagi
// jadi 2 kolom, itu bukan perilaku PHP asli.
public class Kelas
{
    public int KelasId { get; set; }
    public required string NamaKelas { get; set; }
    public required string Tingkat { get; set; }
    public byte? Kelompok { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ArchivedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Siswa> SiswaList { get; set; } = new List<Siswa>();
    public ICollection<WaliKelas> WaliKelasList { get; set; } = new List<WaliKelas>();
    public ICollection<JadwalPelajaran> JadwalPelajaranList { get; set; } = new List<JadwalPelajaran>();
}
