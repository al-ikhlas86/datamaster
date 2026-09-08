namespace DataMaster.Data.Entities;

// mata_pelajaran - Kode boleh NULL berulang (unique constraint hanya menolak
// duplikat NILAI, NULL selalu dianggap beda di SQL standar - sama seperti MySQL).
// Kode dipakai di kode grid Jadwal Pelajaran ("35K" = guru 35 + mapel kode K).
public class MataPelajaran
{
    public int MataPelajaranId { get; set; }
    public required string Nama { get; set; }
    public string? Kode { get; set; }
    public string? Kelompok { get; set; }
    public int Urutan { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<KurikulumAlokasi> KurikulumAlokasiList { get; set; } = new List<KurikulumAlokasi>();
    public ICollection<GuruMataPelajaran> GuruMataPelajaranList { get; set; } = new List<GuruMataPelajaran>();
    public ICollection<JadwalPelajaran> JadwalPelajaranList { get; set; } = new List<JadwalPelajaran>();
}
