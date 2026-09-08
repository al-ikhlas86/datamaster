namespace DataMaster.Data.Entities;

// kalender_akademik - Kategori teks BEBAS (bukan enum), Warna hex opsional dengan
// saran otomatis per kategori (WARNA_SARAN, lihat 03-akademik-jadwal.md §5.5).
// TanggalSelesai NULL = kegiatan 1 hari. Setiap tanggal (manual maupun import)
// WAJIB divalidasi via rentangWajar() di layer aplikasi (buffer ~1 bulan dari
// periode akademik Juli-Juni tahun ajaran) untuk menangkap kesalahan tahun ketik
// SAAT ITU JUGA - lihat bug historis §7 dan §10 poin A di spec yang sama. Validasi
// ini TIDAK bisa berupa CHECK constraint DB karena bergantung pada nama TahunAjaran
// terkait (format "YYYY/YYYY") - harus logic aplikasi.
public class KalenderAkademik
{
    public int KalenderAkademikId { get; set; }
    public int TahunAjaranId { get; set; }
    public required string Judul { get; set; }
    public string? Kategori { get; set; }
    public string? Warna { get; set; }
    public DateOnly TanggalMulai { get; set; }
    public DateOnly? TanggalSelesai { get; set; }
    public string? Waktu { get; set; }
    public string? Sasaran { get; set; }
    public bool IsLibur { get; set; }
    public string? Keterangan { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public TahunAjaran TahunAjaran { get; set; } = null!;
}
