namespace DataMaster.Data.Entities;

// siswa - PENTING: model PHP asli (SiswaModel) sengaja $skipValidation=true, semua
// validasi (regex nama, numeric NIS/NISN/RT/RW, No HP wajib) HANYA di controller
// Siswa::store()/update(). Jalur Import (applyImport) dan PsbProcessor::terima()
// TIDAK melewati validator itu sama sekali - replikasikan: layer aplikasi (bukan
// entity/DbContext) yang menegakkan validasi form manual, sementara jalur import/PSB
// hanya mengecek apa yang eksplisit dicek di PHP (lihat 01-siswa-psb.md §1.2, §3.13, §3.24).
// NoHandphone WAJIB diisi di semua jalur pembuatan siswa AKTIF sejak 2026-09-07,
// TAPI tetap nullable di skema DB (mengikuti kolom asli yang nullable di level MySQL).
public class Siswa
{
    public int SiswaId { get; set; }
    public required string Nama { get; set; }
    public JenisKelamin JenisKelamin { get; set; }
    public required string Nis { get; set; }
    public string? Nisn { get; set; }
    public int? KelasId { get; set; }
    public StatusSiswa Status { get; set; } = StatusSiswa.aktif;
    public string? TempatLahir { get; set; }
    public DateOnly? TanggalLahir { get; set; }
    public string? AsalSekolah { get; set; }
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
    // No HP orang tua KEDUA (ayah ATAUPUN ibu, siapa pun yang kedua) - opsional,
    // ditambahkan 2026-09-11 supaya kedua orang tua bisa punya akun HP sendiri2
    // (login/notifikasi presensi/pembayaran/tugas independen - lihat diskusi
    // poin #11). Berbeda dari NoHandphone (WAJIB, No HP 1) - field ini SELALU
    // opsional di semua jalur (form, import baris insert MAUPUN update).
    public string? NoHandphoneKedua { get; set; }
    public string? DokumenKk { get; set; }
    public string? DokumenAkta { get; set; }
    public string? DokumenKia { get; set; }
    public string? DokumenIjazah { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Kelas? Kelas { get; set; }
    public CalonSiswa? CalonSiswaAsal { get; set; }
    public ICollection<EkskulSiswa> EkskulSiswaList { get; set; } = new List<EkskulSiswa>();
    public ICollection<RiwayatAkademik> RiwayatAkademikList { get; set; } = new List<RiwayatAkademik>();
}
