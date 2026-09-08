namespace DataMaster.Data.Entities;

// tahun_ajaran - nama format wajib "YYYY/YYYY" dipaksa di controller (regex_match),
// BUKAN di validasi model PHP asli (yang cuma required|min4|max20) - lihat
// 02-guru-kelas-struktur.md §9.5. Direplikasi sebagai validasi di layer aplikasi,
// bukan constraint DB, supaya perilakunya tetap identik.
public class TahunAjaran
{
    public int TahunAjaranId { get; set; }
    public required string Nama { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Kelas TIDAK di-scope per tahun ajaran di skema asli (kelas persisten lintas
    // tahun; hanya WaliKelas yang per-tahun) - JANGAN tambahkan navigasi ke Kelas
    // di sini, itu akan membuat EF menyisipkan FK bayangan yang tidak ada di PHP asli.
    public ICollection<Guru> GuruMulai { get; set; } = new List<Guru>();
    public ICollection<WaliKelas> WaliKelasList { get; set; } = new List<WaliKelas>();
    public ICollection<KepalaSekolah> KepalaSekolahList { get; set; } = new List<KepalaSekolah>();
    public ICollection<KurikulumAlokasi> KurikulumAlokasiList { get; set; } = new List<KurikulumAlokasi>();
    public ICollection<JamPelajaran> JamPelajaranList { get; set; } = new List<JamPelajaran>();
    public ICollection<JadwalPelajaran> JadwalPelajaranList { get; set; } = new List<JadwalPelajaran>();
    public ICollection<JadwalPiket> JadwalPiketList { get; set; } = new List<JadwalPiket>();
    public ICollection<KalenderAkademik> KalenderAkademikList { get; set; } = new List<KalenderAkademik>();
    public ICollection<RiwayatAkademik> RiwayatAkademikList { get; set; } = new List<RiwayatAkademik>();
}
