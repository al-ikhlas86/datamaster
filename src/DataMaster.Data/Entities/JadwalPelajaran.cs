namespace DataMaster.Data.Entities;

// jadwal_pelajaran - SETELAH restrukturisasi 2026-08-20: TIDAK punya kolom
// hari/jam_mulai/jam_selesai sendiri lagi, semua itu didapat via JamPelajaranId
// (FK ke JamPelajaran yang sudah mengandung tahun ajaran+hari+jam ke). GuruId
// NULLABLE (mapel tanpa guru tetap, mis. BTAQ Ummi berkelompok). Unique
// (TahunAjaranId,Semester,KelasId,JamPelajaranId) - 1 kelas mustahil 2 pelajaran
// di jam yang sama. Cek bentrok guru: karena JamPelajaranId sudah unik per
// (tahun+hari+jam ke), cukup pencocokan Guru+JamPelajaranId+Semester, TIDAK perlu
// hitung overlap waktu lagi - lihat 03-akademik-jadwal.md §5.4, §10 poin E.
// MataPelajaranId RESTRICT saat dihapus (tidak bisa hapus mapel yang masih dipakai
// jadwal), tapi JamPelajaranId CASCADE (hapus jam ikut menghapus semua jadwal yang
// memakainya) - 2 perilaku FK yang SANGAT BERBEDA, jangan disamakan (§10 poin K).
public class JadwalPelajaran
{
    public int JadwalPelajaranId { get; set; }
    public int TahunAjaranId { get; set; }
    public Semester Semester { get; set; }
    public int KelasId { get; set; }
    public int JamPelajaranId { get; set; }
    public int MataPelajaranId { get; set; }
    public int? GuruId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public TahunAjaran TahunAjaran { get; set; } = null!;
    public Kelas Kelas { get; set; } = null!;
    public JamPelajaran JamPelajaran { get; set; } = null!;
    public MataPelajaran MataPelajaran { get; set; } = null!;
    public Guru? Guru { get; set; }
}
