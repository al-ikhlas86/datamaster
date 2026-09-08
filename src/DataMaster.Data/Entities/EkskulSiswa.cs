namespace DataMaster.Data.Entities;

// ekskul_siswa - TIDAK ADA UpdatedAt (PHP: useTimestamps=false, cuma CreatedAt
// diisi manual). Unique (EkskulId,SiswaId): 1 siswa boleh ikut >1 ekskul beda,
// cuma tidak boleh dobel di ekskul yang sama. Saat menambah peserta, cek duplikat
// harus MANUAL sebelum insert (bukan andalkan exception unique constraint) supaya
// form yang disubmit dobel/lambat tidak berhenti di tengah loop - lihat
// 02-guru-kelas-struktur.md §12 poin 21.
public class EkskulSiswa
{
    public int Id { get; set; }
    public int EkskulId { get; set; }
    public int SiswaId { get; set; }
    public DateOnly TanggalDaftar { get; set; }
    public DateTime? CreatedAt { get; set; }

    public Ekskul Ekskul { get; set; } = null!;
    public Siswa Siswa { get; set; } = null!;
}
