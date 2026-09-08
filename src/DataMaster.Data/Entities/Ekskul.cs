namespace DataMaster.Data.Entities;

// ekskul - mekanisme ABSENSI ekskul SENGAJA BELUM ADA (ditunda user secara
// eksplisit di percakapan aslinya) - modul ini murni kelola daftar + peserta.
// Pembina adalah teks BEBAS, bukan FK ke Guru (bisa pelatih eksternal).
public class Ekskul
{
    public int EkskulId { get; set; }
    public required string Nama { get; set; }
    public string? Pembina { get; set; }
    public string? Hari { get; set; }
    public string? Deskripsi { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ArchivedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<EkskulSiswa> EkskulSiswaList { get; set; } = new List<EkskulSiswa>();
}
