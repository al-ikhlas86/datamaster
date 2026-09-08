namespace DataMaster.Data.Entities;

// jadwal_piket - jumlah guru piket per hari BEBAS (tidak dikunci ke 2 atau angka
// tetap manapun). Simpan selalu pola "replace all" (hapus semua baris untuk
// TahunAjaranId+Semester lalu insert ulang), BUKAN diff - lihat
// 03-akademik-jadwal.md §4 (simpanPiket()).
public class JadwalPiket
{
    public int JadwalPiketId { get; set; }
    public int TahunAjaranId { get; set; }
    public Semester Semester { get; set; }
    public Hari Hari { get; set; }
    public int GuruId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public TahunAjaran TahunAjaran { get; set; } = null!;
    public Guru Guru { get; set; } = null!;
}
