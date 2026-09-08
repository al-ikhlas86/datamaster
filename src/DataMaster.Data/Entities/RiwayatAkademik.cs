namespace DataMaster.Data.Entities;

// riwayat_akademik - arsip PERMANEN, backend PHP asli SENGAJA selalu menolak bulk
// delete pada data ini ("Riwayat akademik tidak dapat dihapus karena dipakai
// sebagai arsip permanen.") - replikasikan larangan ini di service layer, JANGAN
// sediakan operasi delete untuk entity ini kecuali diminta eksplisit. Unique
// (SiswaId,TahunAjaranId) - 1 siswa cuma 1 record per tahun ajaran. KelasId TIDAK
// PUNYA FK eksplisit di PHP asli (murni snapshot "kelas siswa pada tahun itu") -
// bisa berisi 0 (literal, bukan null) untuk siswa yang kelas_id-nya null saat
// diluluskan (lihat 03-akademik-jadwal.md §9 prosesKelulusan(), §10 poin P) -
// KelasId di sini dibuat NULLABLE (bukan default 0) supaya representasinya lebih
// benar di .NET; layer migrasi data dari PHP harus memetakan nilai 0 lama ke NULL.
public class RiwayatAkademik
{
    public int RiwayatAkademikId { get; set; }
    public int SiswaId { get; set; }
    public int TahunAjaranId { get; set; }
    public int? KelasId { get; set; }
    public StatusRiwayatAkademik Status { get; set; } = StatusRiwayatAkademik.aktif;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Siswa Siswa { get; set; } = null!;
    public TahunAjaran TahunAjaran { get; set; } = null!;
}
