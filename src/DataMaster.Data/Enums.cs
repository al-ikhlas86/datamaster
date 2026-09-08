namespace DataMaster.Data;

// Semua enum di sini di-map ke TEXT di SQLite (HasConversion<string>() di DbContext),
// bukan ke integer - supaya nilai di file .db tetap terbaca manusia persis seperti
// ENUM MySQL asli (mis. lihat langsung isi tabel tanpa perlu tabel lookup terpisah),
// dan supaya nama constant di C# ini SAMA PERSIS string yang dikirim/diterima Hub API.

public enum JenisKelamin
{
    L,
    P,
}

// siswa.status - migrasi 2026-08-04 menambah 'pindah'/'keluar' tapi 'pindah' sudah
// tidak dipakai controller manapun (lihat 01-siswa-psb.md §1.1 poin 8) - tetap
// direplikasi apa adanya, jangan dihapus dari model.
public enum StatusSiswa
{
    aktif,
    lulus,
    pindah,
    keluar,
}

public enum StatusCalonSiswa
{
    menunggu,
    diterima,
    ditolak,
}

// guru.jabatan - guru_kelas/guru_bidang TIDAK PERNAH diketik manual, derivasi
// otomatis dari WaliKelasModel::sinkronJabatanGuru() (lihat 02-guru-kelas-struktur.md §11).
public enum JabatanGuru
{
    guru_kelas,
    guru_bidang,
    karyawan,
}

// guru.status_keluar - diisi TU saat mengarsipkan (opsional), null = masih aktif
// ATAU diarsipkan sebelum kolom ini ada (2026-09-07).
public enum StatusKeluarGuru
{
    purna_bakti,
    resign,
    diberhentikan,
}

public enum Semester
{
    ganjil,
    genap,
}

public enum Hari
{
    senin,
    selasa,
    rabu,
    kamis,
    jumat,
    sabtu,
    minggu,
}

// jam_pelajaran.jenis - 'kegiatan' (Istirahat/Ishoma/Upacara) tidak bisa diisi
// lewat grid/import Jadwal Pelajaran, otomatis muncul di semua kelas.
public enum JenisJamPelajaran
{
    pelajaran,
    kegiatan,
}

// riwayat_akademik.status
public enum StatusRiwayatAkademik
{
    aktif,
    naik,
    lulus,
}
