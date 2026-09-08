namespace DataMaster.Data.Entities;

// kepala_sekolah - BUKAN jabatan/role terpisah, hanya penanda tambahan di atas
// guru/pegawai yang sudah ada (pola sama Wali Kelas tapi cuma 1 slot per tahun
// ajaran, bukan per-kelas, dan TANPA syarat jabatan tertentu - kandidatnya SEMUA
// guru/pegawai aktif: guru_kelas/guru_bidang/karyawan). Percobaan pertama memakai
// Jabatan enum ke-4 DIBATALKAN karena merusak Hub API & role dasar guru. "Maks 1
// baris per tahun ajaran" dijaga di service layer (tetapkan() selalu hapus baris
// lama tahun itu dulu sebelum insert baru), UNIQUE(GuruId,TahunAjaranId) di DB
// murni jaga-jaga, bukan mekanisme utama. PENTING (gap nyata belum diperbaiki di
// PHP asli): tidak ada guard yang otomatis mengosongkan slot ini saat Guru terkait
// dinonaktifkan (StatusAktif=false) - keputusan desain saat porting: replikasi
// apa adanya, atau tambahkan filter/auto-kosongkan. Lihat 02-guru-kelas-struktur.md
// §6 untuk detail lengkap (index/cariKandidat/tetapkan/kosongkan + integrasi sync).
public class KepalaSekolah
{
    public int KepalaSekolahId { get; set; }
    public int GuruId { get; set; }
    public int TahunAjaranId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Guru Guru { get; set; } = null!;
    public TahunAjaran TahunAjaran { get; set; } = null!;
}
