namespace DataMaster.Data.Entities;

// tingkat - master jenjang (Playgroup/TK-A/TK-B/Kelas 1-6 dst). `Kode` adalah
// kunci relasi TEKS dipakai kelas.tingkat, kurikulum_alokasi.tingkat_kode,
// guru_mata_pelajaran.tingkat - BUKAN FK numerik ke TingkatId (lihat
// 03-akademik-jadwal.md §10 poin I). Jangan ganti pola ini jadi FK numerik biasa,
// karena banyak tempat menyimpan Kode sebagai string lepas tanpa constraint DB -
// penghapusan tingkat harus dicek manual (COUNT pemakaian), bukan lewat FK RESTRICT.
public class Tingkat
{
    public int TingkatId { get; set; }
    public required string Kode { get; set; }
    public required string Nama { get; set; }
    public int Urutan { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
