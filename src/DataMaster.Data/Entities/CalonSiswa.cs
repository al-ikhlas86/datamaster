namespace DataMaster.Data.Entities;

// calon_siswa - SENGAJA tabel terpisah dari Siswa (bukan status baru di Siswa),
// karena Siswa otomatis ikut sync ke Hub API/statistik, calon yang belum diterima
// tidak boleh ikut. Baris ini TETAP disimpan setelah diterima (SiswaId terisi) -
// jejak riwayat pendaftaran, dan dokumen TIDAK disalin ulang ke baris Siswa baru
// (Siswa hanya menunjuk nama file yang sama). Lihat 01-siswa-psb.md §1.4.
// NoHandphone di sini TETAP opsional (beda dari Siswa yang wajib).
public class CalonSiswa
{
    public int CalonSiswaId { get; set; }
    public required string Nama { get; set; }
    public JenisKelamin JenisKelamin { get; set; }
    public string? Nisn { get; set; }
    public string? TempatLahir { get; set; }
    public DateOnly? TanggalLahir { get; set; }
    public string? AsalSekolah { get; set; }
    public string? TingkatDituju { get; set; }
    public string? AlamatJalan { get; set; }
    public string? AlamatRt { get; set; }
    public string? AlamatRw { get; set; }
    public string? AlamatKelurahan { get; set; }
    public string? AlamatKecamatan { get; set; }
    public string? NamaAyah { get; set; }
    public string? PekerjaanAyah { get; set; }
    public string? NamaIbu { get; set; }
    public string? PekerjaanIbu { get; set; }
    public string? NoHandphone { get; set; }
    public string? DokumenKk { get; set; }
    public string? DokumenAkta { get; set; }
    public string? DokumenKia { get; set; }
    public string? DokumenIjazah { get; set; }
    public string? Catatan { get; set; }
    public StatusCalonSiswa Status { get; set; } = StatusCalonSiswa.menunggu;
    public int? SiswaId { get; set; }
    // Kontinuitas TK->SD (2026-09-11, poin #12) - id baris sync_students di Hub
    // API (BUKAN id lokal apa pun) asal baris ini diimpor, kalau dibuat lewat
    // "Impor Lulusan TK" (CalonSiswaController.ImporLulusanTk). Null utk
    // pendaftaran PSB biasa. Dipakai MENCEGAH IMPOR DOBEL - sebelum menampilkan
    // daftar lulusan dari Hub API, baris yang SumberLulusanId-nya sudah cocok
    // disembunyikan dari daftar (sudah pernah diimpor).
    public int? SumberLulusanId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Siswa? Siswa { get; set; }
}
