namespace DataMaster.Data.Entities;

// kurikulum_alokasi - baris TIDAK ADA berarti mapel TIDAK diajarkan di tingkat itu.
// JpPerMinggu 0/kosong berarti HAPUS baris (bukan simpan 0) - menjaga beda makna
// "tidak diajarkan" vs "diajarkan 0 jam". Layer aplikasi (bukan constraint DB) yang
// menegakkan aturan hapus-jika-nol ini - lihat 03-akademik-jadwal.md §5.3, §10 poin H.
public class KurikulumAlokasi
{
    public int KurikulumAlokasiId { get; set; }
    public int TahunAjaranId { get; set; }
    public required string TingkatKode { get; set; }
    public int MataPelajaranId { get; set; }
    public int JpPerMinggu { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public TahunAjaran TahunAjaran { get; set; } = null!;
    public MataPelajaran MataPelajaran { get; set; } = null!;
}
