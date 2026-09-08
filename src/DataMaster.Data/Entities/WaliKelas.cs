namespace DataMaster.Data.Entities;

// wali_kelas - VERSIONED PER TAHUN AJARAN (sejak 2026-09-04), bukan cuma
// current-state. Unique (GuruId,TahunAjaranId): 1 guru cuma wali 1 kelas per tahun.
// Unique (KelasId,TahunAjaranId,Urutan): 1 slot per kelas per tahun cuma 1 guru.
// Urutan TIDAK dibatasi maksimal (histori: 1:1 ketat -> maks 2 -> bebas jumlahnya).
// SEMUA mutasi (tetapkan/tetapkanSemua) HARUS lewat service yang mereplikasi
// WaliKelasModel PHP - termasuk logic "hapus guru ini dari kelas lain di tahun yang
// sama sebelum insert baru" dan "sinkronisasi guru.jabatan HANYA kalau tahun yang
// diedit = tahun ajaran aktif". Lihat 02-guru-kelas-struktur.md §11.
public class WaliKelas
{
    public int WaliKelasId { get; set; }
    public int KelasId { get; set; }
    public int GuruId { get; set; }
    public int TahunAjaranId { get; set; }
    public byte Urutan { get; set; } = 1;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Kelas Kelas { get; set; } = null!;
    public Guru Guru { get; set; } = null!;
    public TahunAjaran TahunAjaran { get; set; } = null!;
}
