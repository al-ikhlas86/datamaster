namespace DataMaster.Data.Entities;

// rencana_kenaikan - staging area fitur "Tahun Ajaran Kerja" (2026-09-25): rencana
// kenaikan/kelulusan yang disusun TU SAAT kerja di tahun ajaran yang BELUM aktif -
// TIDAK menyentuh Siswa.KelasId sama sekali sampai TahunAjaranController.SetActive
// menerapkannya (lihat komentar SetActive). Unique (SiswaId,TahunAjaranTujuanId) -
// upsert kalau TU revisi rencana yang sudah ada, bukan numpuk baris. Baris di sini
// SELALU terhapus begitu diterapkan (bukan arsip - RiwayatAkademik yang jadi arsip
// permanennya, dibuat saat penerapan).
public class RencanaKenaikan
{
    public int RencanaKenaikanId { get; set; }
    public int TahunAjaranTujuanId { get; set; }
    public int SiswaId { get; set; }
    public bool Lulus { get; set; } // true = lulus (KelasTujuanId harus null), false = naik kelas
    public int? KelasTujuanId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Siswa Siswa { get; set; } = null!;
    public TahunAjaran TahunAjaranTujuan { get; set; } = null!;
}
