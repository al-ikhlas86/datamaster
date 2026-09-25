namespace DataMaster.Data.Entities;

// penilaian_sikap - grade sikap (perilaku) siswa per semester, dasar utk fitur
// acak kenaikan kelas (AkademikController.GenerateRandomKenaikan): siswa dgn
// grade kumulatif TERBURUK (perlu_bimbingan) disebar round-robin ke tiap kelas
// tujuan supaya tidak numpuk 1 kelas, sisanya benar2 acak. Kumulatif per tahun
// ajaran dihitung dari rata2 Semester ganjil+genap yang sudah diisi (lihat
// AkademikController - TIDAK disimpan sbg kolom terpisah, dihitung on-the-fly
// supaya selalu konsisten dgn input terakhir). Unique (SiswaId,TahunAjaranId,Semester)
// - 1 siswa cuma 1 grade per semester per tahun ajaran.
public class PenilaianSikap
{
    public int PenilaianSikapId { get; set; }
    public int SiswaId { get; set; }
    public int TahunAjaranId { get; set; }
    public Semester Semester { get; set; }
    public GradeSikap Grade { get; set; }
    public string? Catatan { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Siswa Siswa { get; set; } = null!;
    public TahunAjaran TahunAjaran { get; set; } = null!;
}
