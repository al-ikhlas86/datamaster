namespace DataMaster.Data.Entities;

// guru_mata_pelajaran - Tingkat NULL = berlaku UNIVERSAL semua tingkat; kalau diisi
// HARUS sama persis dgn Kelas.Tingkat/Tingkat.Kode. Unique constraint WAJIB PERSIS
// (GuruId, MataPelajaranId, Tingkat) - JANGAN tambahkan unique (GuruId,MataPelajaranId)
// terpisah tanpa Tingkat, itu bug yang sudah diperbaiki di PHP asli (migrasi
// DropOldGuruMataPelajaranUniqueKey) karena menolak 1 guru mengampu mapel sama di
// 2 tingkat berbeda. Lihat 02-guru-kelas-struktur.md §1.9 dan §12 poin 8.
public class GuruMataPelajaran
{
    public int GuruMataPelajaranId { get; set; }
    public int GuruId { get; set; }
    public int MataPelajaranId { get; set; }
    public string? Tingkat { get; set; }
    public DateTime? CreatedAt { get; set; }

    public Guru Guru { get; set; } = null!;
    public MataPelajaran MataPelajaran { get; set; } = null!;
}
