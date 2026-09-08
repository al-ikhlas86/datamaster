using DataMaster.Data;
using DataMaster.Data.Entities;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

// Alat migrasi SATU ARAH: MySQL (webarsipdata_db, web PHP CodeIgniter4 asli) ->
// SQLite (database aplikasi desktop C# ini). Dipakai SEKALI saat PC TU SD
// dipindahkan dari web PHP ke aplikasi desktop - JANGAN dijalankan berulang ke
// database SQLite yang sudah berisi data asli (akan bentrok unique constraint).
//
// LINGKUP SENGAJA TIDAK TERMASUK tabel users/auth_groups/auth_groups_users:
// password_hash PHP (algoritma lama) TIDAK KOMPATIBEL dengan PasswordHasher<User>
// .NET yang dipakai aplikasi desktop (lihat komentar User.cs entity: "user PHP
// lama tidak bisa dipindah otomatis, harus reset password saat migrasi pertama
// kali"). Aplikasi ini juga TIDAK punya fitur "lupa password" (sengaja tidak
// diporting - lihat 04-infra-auth-sync.md), jadi memindahkan hash password yang
// tidak bisa dipakai sama saja mengunci TU keluar total tanpa jalan keluar. Solusi
// yang benar: SETELAH migrasi data ini selesai, halaman "Setup Awal" TIDAK akan
// muncul lagi (Users sudah tidak kosong kalau ikut dimigrasi) - jadi user/admin
// harus dibuat scara manual lewat menu Kelola User oleh admin yang login duluan.
// Solusi paling sederhana: migrasi HANYA data sekolah (siswa/guru/kelas/dst),
// lalu jalankan Setup Awal seperti PC baru (Users memang kosong) - itulah kenapa
// tabel users TIDAK disentuh sama sekali oleh alat ini.
//
// system_settings JUGA TIDAK dimigrasi - itu murni konfigurasi operasional
// (jadwal backup online, dst), bukan data sekolah, dan mekanismenya beda total
// antara PHP (mysqldump terjadwal) dan desktop (VACUUM INTO) - biarkan default.
//
// Cara pakai:
//   dotnet run --project src/DataMaster.MigrationTool -- \
//     --mysql "Server=localhost;Database=webarsipdata_db;User=root;Password=;" \
//     --sqlite "C:\path\ke\datamaster.db"
//
// Output SQLite HARUS file baru/kosong (belum pernah diisi Setup Awal ataupun
// data lain) - alat ini akan membuat skema dari nol via EF Core migrations kalau
// filenya belum ada, tapi TIDAK akan menimpa/membersihkan file yang sudah berisi
// data (lihat pengecekan "sudah ada data" di awal Main).

var mysqlConn = GetArg(args, "--mysql") ?? throw new ArgumentException("--mysql wajib diisi (connection string MySQL sumber).");
var sqlitePath = GetArg(args, "--sqlite") ?? throw new ArgumentException("--sqlite wajib diisi (path file .db tujuan).");

Console.WriteLine("=== Alat Migrasi Data Master: MySQL (PHP) -> SQLite (Desktop) ===");
Console.WriteLine($"Sumber MySQL : {MaskPassword(mysqlConn)}");
Console.WriteLine($"Tujuan SQLite: {sqlitePath}");
Console.WriteLine();

var optionsBuilder = new DbContextOptionsBuilder<DataMasterDbContext>();
optionsBuilder.UseSqlite($"Data Source={sqlitePath}");
await using var db = new DataMasterDbContext(optionsBuilder.Options);

Console.WriteLine("Menyiapkan skema SQLite tujuan (migrate)...");
await db.Database.MigrateAsync();

// Pengaman WAJIB - tolak migrasi kalau tujuan sudah berisi data sekolah (mencegah
// menjalankan alat ini 2x atau ke database yang sudah dipakai produksi, yang akan
// menghasilkan duplikat/bentrok unique constraint di tengah jalan).
if (await db.TahunAjaran.AnyAsync() || await db.Siswa.AnyAsync() || await db.Guru.AnyAsync())
{
    Console.Error.WriteLine("BERHENTI: database SQLite tujuan sudah berisi data (Tahun Ajaran/Siswa/Guru bukan kosong).");
    Console.Error.WriteLine("Alat ini HANYA aman dijalankan ke database yang benar-benar kosong/baru.");
    return 1;
}

await using var mysql = new MySqlConnection(mysqlConn);
await mysql.OpenAsync();

var laporan = new List<(string Tabel, int Sumber, int Tujuan)>();

async Task<int> HitungMysql(string tabel)
{
    await using var cmd = new MySqlCommand($"SELECT COUNT(*) FROM `{tabel}`", mysql);
    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
}

async Task<List<Dictionary<string, object?>>> BacaSemua(string tabel, string orderBy)
{
    var rows = new List<Dictionary<string, object?>>();
    await using var cmd = new MySqlCommand($"SELECT * FROM `{tabel}` ORDER BY `{orderBy}`", mysql);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var row = new Dictionary<string, object?>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var val = reader.GetValue(i);
            row[reader.GetName(i)] = val is DBNull ? null : val;
        }
        rows.Add(row);
    }
    return rows;
}

// --- Helper baca nilai per tipe, toleran NULL ---
string? S(Dictionary<string, object?> r, string k) => r[k] as string;
int I(Dictionary<string, object?> r, string k) => Convert.ToInt32(r[k]);
int? IN(Dictionary<string, object?> r, string k) => r[k] is null ? null : Convert.ToInt32(r[k]);
bool B(Dictionary<string, object?> r, string k) => Convert.ToInt64(r[k]) != 0;
DateTime? DT(Dictionary<string, object?> r, string k) => r[k] is null ? null : Convert.ToDateTime(r[k]);
DateOnly? DO(Dictionary<string, object?> r, string k) => r[k] is null ? null : DateOnly.FromDateTime(Convert.ToDateTime(r[k]));
TimeOnly TO(Dictionary<string, object?> r, string k) => TimeOnly.FromTimeSpan((TimeSpan)r[k]!);
T? Enm<T>(Dictionary<string, object?> r, string k) where T : struct, Enum =>
    r[k] is null ? null : Enum.Parse<T>((string)r[k]!);

// ================= 1. Tahun Ajaran =================
Console.WriteLine("Migrasi Tahun Ajaran...");
var taRows = await BacaSemua("tahun_ajaran", "tahun_ajaran_id");
var petaTa = new Dictionary<int, int>();
foreach (var r in taRows)
{
    var e = new TahunAjaran { Nama = S(r, "nama")!, IsActive = B(r, "is_active"), CreatedAt = DT(r, "created_at"), UpdatedAt = DT(r, "updated_at") };
    db.TahunAjaran.Add(e);
    await db.SaveChangesAsync();
    petaTa[I(r, "tahun_ajaran_id")] = e.TahunAjaranId;
}
laporan.Add(("tahun_ajaran", await HitungMysql("tahun_ajaran"), await db.TahunAjaran.CountAsync()));

// ================= 2. Tingkat =================
Console.WriteLine("Migrasi Tingkat...");
var tkRows = await BacaSemua("tingkat", "tingkat_id");
foreach (var r in tkRows)
{
    db.Tingkat.Add(new Tingkat { Kode = S(r, "kode")!, Nama = S(r, "nama")!, Urutan = I(r, "urutan"), IsActive = B(r, "is_active"), CreatedAt = DT(r, "created_at"), UpdatedAt = DT(r, "updated_at") });
}
await db.SaveChangesAsync();
laporan.Add(("tingkat", await HitungMysql("tingkat"), await db.Tingkat.CountAsync()));

// ================= 3. Kelas =================
Console.WriteLine("Migrasi Kelas...");
var kelasRows = await BacaSemua("kelas", "kelas_id");
var petaKelas = new Dictionary<int, int>();
foreach (var r in kelasRows)
{
    var e = new Kelas
    {
        NamaKelas = S(r, "nama_kelas")!,
        Tingkat = S(r, "tingkat")!,
        Kelompok = r["kelompok"] is null ? null : Convert.ToByte(r["kelompok"]),
        IsActive = B(r, "is_active"),
        ArchivedAt = DT(r, "archived_at"),
        CreatedAt = DT(r, "created_at"),
        UpdatedAt = DT(r, "updated_at"),
    };
    db.Kelas.Add(e);
    await db.SaveChangesAsync();
    petaKelas[I(r, "kelas_id")] = e.KelasId;
}
laporan.Add(("kelas", await HitungMysql("kelas"), await db.Kelas.CountAsync()));

// ================= 4. Guru =================
Console.WriteLine("Migrasi Guru...");
var guruRows = await BacaSemua("guru", "guru_id");
var petaGuru = new Dictionary<int, int>();
foreach (var r in guruRows)
{
    var e = new Guru
    {
        Nama = S(r, "nama")!,
        Nip = S(r, "nip"),
        NomorUrut = IN(r, "nomor_urut"),
        JenisKelamin = Enm<JenisKelamin>(r, "jenis_kelamin"),
        Jabatan = Enum.Parse<JabatanGuru>(S(r, "jabatan")!),
        MataPelajaranCatatan = S(r, "mata_pelajaran"),
        JumlahJamMengajar = r["jumlah_jam_mengajar"] is null ? null : Convert.ToInt16(r["jumlah_jam_mengajar"]),
        GelarTerakhir = S(r, "gelar_terakhir"),
        NoHandphone = S(r, "no_handphone"),
        Alamat = S(r, "alamat"),
        StatusAktif = B(r, "status_aktif"),
        StatusKeluar = Enm<StatusKeluarGuru>(r, "status_keluar"),
        // FK ke TahunAjaran diisi belakangan (butuh peta lengkap dulu) - lihat bawah.
        CreatedAt = DT(r, "created_at"),
        UpdatedAt = DT(r, "updated_at"),
    };
    db.Guru.Add(e);
    await db.SaveChangesAsync();
    petaGuru[I(r, "guru_id")] = e.GuruId;

    var taMulaiLama = IN(r, "tahun_ajaran_mulai_id");
    if (taMulaiLama is not null && petaTa.TryGetValue(taMulaiLama.Value, out var taMulaiBaru))
    {
        e.TahunAjaranMulaiId = taMulaiBaru;
        await db.SaveChangesAsync();
    }
}
laporan.Add(("guru", await HitungMysql("guru"), await db.Guru.CountAsync()));

// ================= 5. Mata Pelajaran =================
Console.WriteLine("Migrasi Mata Pelajaran...");
var mapelRows = await BacaSemua("mata_pelajaran", "mata_pelajaran_id");
var petaMapel = new Dictionary<int, int>();
foreach (var r in mapelRows)
{
    var e = new MataPelajaran { Nama = S(r, "nama")!, Kode = S(r, "kode"), Kelompok = S(r, "kelompok"), Urutan = IN(r, "urutan") ?? 0, CreatedAt = DT(r, "created_at"), UpdatedAt = DT(r, "updated_at") };
    db.MataPelajaran.Add(e);
    await db.SaveChangesAsync();
    petaMapel[I(r, "mata_pelajaran_id")] = e.MataPelajaranId;
}
laporan.Add(("mata_pelajaran", await HitungMysql("mata_pelajaran"), await db.MataPelajaran.CountAsync()));

// ================= 6. Guru Mata Pelajaran =================
Console.WriteLine("Migrasi Guru Mata Pelajaran...");
var gmpRows = await BacaSemua("guru_mata_pelajaran", "guru_mata_pelajaran_id");
var dilewatiGmp = 0;
foreach (var r in gmpRows)
{
    var guruLama = I(r, "guru_id");
    var mapelLama = I(r, "mata_pelajaran_id");
    if (!petaGuru.TryGetValue(guruLama, out var guruBaru) || !petaMapel.TryGetValue(mapelLama, out var mapelBaru)) { dilewatiGmp++; continue; }
    db.GuruMataPelajaran.Add(new GuruMataPelajaran { GuruId = guruBaru, MataPelajaranId = mapelBaru, Tingkat = S(r, "tingkat"), CreatedAt = DT(r, "created_at") });
}
await db.SaveChangesAsync();
laporan.Add(("guru_mata_pelajaran", await HitungMysql("guru_mata_pelajaran"), await db.GuruMataPelajaran.CountAsync()));
if (dilewatiGmp > 0) Console.WriteLine($"  PERINGATAN: {dilewatiGmp} baris guru_mata_pelajaran dilewati (guru/mapel rujukan tidak ketemu).");

// ================= 7. Jam Pelajaran =================
Console.WriteLine("Migrasi Jam Pelajaran...");
var jamRows = await BacaSemua("jam_pelajaran", "jam_pelajaran_id");
var petaJam = new Dictionary<int, int>();
var dilewatiJam = 0;
foreach (var r in jamRows)
{
    var taLama = I(r, "tahun_ajaran_id");
    if (!petaTa.TryGetValue(taLama, out var taBaru)) { dilewatiJam++; continue; }
    var e = new JamPelajaran
    {
        TahunAjaranId = taBaru,
        Hari = Enum.Parse<Hari>(S(r, "hari")!),
        JamKe = I(r, "jam_ke"),
        JamMulai = TO(r, "jam_mulai"),
        JamSelesai = TO(r, "jam_selesai"),
        Jenis = Enum.Parse<JenisJamPelajaran>(S(r, "jenis")!),
        Label = S(r, "label"),
        CreatedAt = DT(r, "created_at"),
        UpdatedAt = DT(r, "updated_at"),
    };
    db.JamPelajaran.Add(e);
    await db.SaveChangesAsync();
    petaJam[I(r, "jam_pelajaran_id")] = e.JamPelajaranId;
}
laporan.Add(("jam_pelajaran", await HitungMysql("jam_pelajaran"), await db.JamPelajaran.CountAsync()));
if (dilewatiJam > 0) Console.WriteLine($"  PERINGATAN: {dilewatiJam} baris jam_pelajaran dilewati (tahun ajaran rujukan tidak ketemu).");

// ================= 8. Jam Pelajaran Tingkat =================
Console.WriteLine("Migrasi Jam Pelajaran Tingkat...");
var jptRows = await BacaSemua("jam_pelajaran_tingkat", "jam_pelajaran_tingkat_id");
var dilewatiJpt = 0;
foreach (var r in jptRows)
{
    var jamLama = I(r, "jam_pelajaran_id");
    if (!petaJam.TryGetValue(jamLama, out var jamBaru)) { dilewatiJpt++; continue; }
    db.JamPelajaranTingkat.Add(new JamPelajaranTingkat { JamPelajaranId = jamBaru, TingkatKode = S(r, "tingkat_kode")! });
}
await db.SaveChangesAsync();
laporan.Add(("jam_pelajaran_tingkat", await HitungMysql("jam_pelajaran_tingkat"), await db.JamPelajaranTingkat.CountAsync()));
if (dilewatiJpt > 0) Console.WriteLine($"  PERINGATAN: {dilewatiJpt} baris jam_pelajaran_tingkat dilewati.");

// ================= 9. Kurikulum Alokasi =================
Console.WriteLine("Migrasi Kurikulum Alokasi...");
var alokasiRows = await BacaSemua("kurikulum_alokasi", "kurikulum_alokasi_id");
var dilewatiAlokasi = 0;
foreach (var r in alokasiRows)
{
    var taLama = I(r, "tahun_ajaran_id");
    var mapelLama = I(r, "mata_pelajaran_id");
    if (!petaTa.TryGetValue(taLama, out var taBaru) || !petaMapel.TryGetValue(mapelLama, out var mapelBaru)) { dilewatiAlokasi++; continue; }
    db.KurikulumAlokasi.Add(new KurikulumAlokasi { TahunAjaranId = taBaru, TingkatKode = S(r, "tingkat_kode")!, MataPelajaranId = mapelBaru, JpPerMinggu = I(r, "jp_per_minggu"), CreatedAt = DT(r, "created_at"), UpdatedAt = DT(r, "updated_at") });
}
await db.SaveChangesAsync();
laporan.Add(("kurikulum_alokasi", await HitungMysql("kurikulum_alokasi"), await db.KurikulumAlokasi.CountAsync()));
if (dilewatiAlokasi > 0) Console.WriteLine($"  PERINGATAN: {dilewatiAlokasi} baris kurikulum_alokasi dilewati.");

// ================= 10. Wali Kelas =================
Console.WriteLine("Migrasi Wali Kelas...");
var wkRows = await BacaSemua("wali_kelas", "wali_kelas_id");
var dilewatiWk = 0;
foreach (var r in wkRows)
{
    var kelasLama = I(r, "kelas_id");
    var guruLama = I(r, "guru_id");
    var taLama = I(r, "tahun_ajaran_id");
    if (!petaKelas.TryGetValue(kelasLama, out var kelasBaru) || !petaGuru.TryGetValue(guruLama, out var guruBaru) || !petaTa.TryGetValue(taLama, out var taBaru)) { dilewatiWk++; continue; }
    db.WaliKelas.Add(new WaliKelas { KelasId = kelasBaru, GuruId = guruBaru, TahunAjaranId = taBaru, Urutan = Convert.ToByte(r["urutan"]), CreatedAt = DT(r, "created_at"), UpdatedAt = DT(r, "updated_at") });
}
await db.SaveChangesAsync();
laporan.Add(("wali_kelas", await HitungMysql("wali_kelas"), await db.WaliKelas.CountAsync()));
if (dilewatiWk > 0) Console.WriteLine($"  PERINGATAN: {dilewatiWk} baris wali_kelas dilewati.");

// ================= 11. Kepala Sekolah =================
Console.WriteLine("Migrasi Kepala Sekolah...");
var ksRows = await BacaSemua("kepala_sekolah", "kepala_sekolah_id");
var dilewatiKs = 0;
foreach (var r in ksRows)
{
    var guruLama = I(r, "guru_id");
    var taLama = I(r, "tahun_ajaran_id");
    if (!petaGuru.TryGetValue(guruLama, out var guruBaru) || !petaTa.TryGetValue(taLama, out var taBaru)) { dilewatiKs++; continue; }
    db.KepalaSekolah.Add(new KepalaSekolah { GuruId = guruBaru, TahunAjaranId = taBaru, CreatedAt = DT(r, "created_at"), UpdatedAt = DT(r, "updated_at") });
}
await db.SaveChangesAsync();
laporan.Add(("kepala_sekolah", await HitungMysql("kepala_sekolah"), await db.KepalaSekolah.CountAsync()));
if (dilewatiKs > 0) Console.WriteLine($"  PERINGATAN: {dilewatiKs} baris kepala_sekolah dilewati.");

// ================= 12. Siswa =================
Console.WriteLine("Migrasi Siswa...");
var siswaRows = await BacaSemua("siswa", "siswa_id");
var petaSiswa = new Dictionary<int, int>();
foreach (var r in siswaRows)
{
    var kelasLama = IN(r, "kelas_id");
    int? kelasBaru = kelasLama is not null && petaKelas.TryGetValue(kelasLama.Value, out var kb) ? kb : null;
    var e = new Siswa
    {
        Nama = S(r, "nama")!,
        JenisKelamin = Enum.Parse<JenisKelamin>(S(r, "jenis_kelamin")!),
        Nis = S(r, "nis")!,
        Nisn = S(r, "nisn"),
        KelasId = kelasBaru,
        Status = Enum.Parse<StatusSiswa>(S(r, "status")!),
        TempatLahir = S(r, "tempat_lahir"),
        TanggalLahir = DO(r, "tanggal_lahir"),
        AsalSekolah = S(r, "asal_sekolah"),
        AlamatJalan = S(r, "alamat_jalan"),
        AlamatRt = S(r, "alamat_rt"),
        AlamatRw = S(r, "alamat_rw"),
        AlamatKelurahan = S(r, "alamat_kelurahan"),
        AlamatKecamatan = S(r, "alamat_kecamatan"),
        NamaAyah = S(r, "nama_ayah"),
        PekerjaanAyah = S(r, "pekerjaan_ayah"),
        NamaIbu = S(r, "nama_ibu"),
        PekerjaanIbu = S(r, "pekerjaan_ibu"),
        NoHandphone = S(r, "no_handphone"),
        DokumenKk = S(r, "dokumen_kk"),
        DokumenAkta = S(r, "dokumen_akta"),
        DokumenKia = S(r, "dokumen_kia"),
        DokumenIjazah = S(r, "dokumen_ijazah"),
        CreatedAt = DT(r, "created_at"),
        UpdatedAt = DT(r, "updated_at"),
    };
    db.Siswa.Add(e);
    await db.SaveChangesAsync();
    petaSiswa[I(r, "siswa_id")] = e.SiswaId;
}
laporan.Add(("siswa", await HitungMysql("siswa"), await db.Siswa.CountAsync()));
Console.WriteLine("  CATATAN: dokumen (KK/Akta/KIA/Ijazah) di sini cuma NAMA FILE - berkas fisiknya (folder uploads PHP) harus disalin manual terpisah ke folder App_Data/uploads/dokumen aplikasi desktop, alat ini TIDAK menyalin file.");

// ================= 13. Calon Siswa =================
Console.WriteLine("Migrasi Calon Siswa...");
var csRows = await BacaSemua("calon_siswa", "calon_siswa_id");
foreach (var r in csRows)
{
    var siswaLama = IN(r, "siswa_id");
    int? siswaBaru = siswaLama is not null && petaSiswa.TryGetValue(siswaLama.Value, out var sb) ? sb : null;
    db.CalonSiswa.Add(new CalonSiswa
    {
        Nama = S(r, "nama")!,
        JenisKelamin = Enum.Parse<JenisKelamin>(S(r, "jenis_kelamin")!),
        Nisn = S(r, "nisn"),
        TempatLahir = S(r, "tempat_lahir"),
        TanggalLahir = DO(r, "tanggal_lahir"),
        AsalSekolah = S(r, "asal_sekolah"),
        TingkatDituju = S(r, "tingkat_dituju"),
        AlamatJalan = S(r, "alamat_jalan"),
        AlamatRt = S(r, "alamat_rt"),
        AlamatRw = S(r, "alamat_rw"),
        AlamatKelurahan = S(r, "alamat_kelurahan"),
        AlamatKecamatan = S(r, "alamat_kecamatan"),
        NamaAyah = S(r, "nama_ayah"),
        PekerjaanAyah = S(r, "pekerjaan_ayah"),
        NamaIbu = S(r, "nama_ibu"),
        PekerjaanIbu = S(r, "pekerjaan_ibu"),
        NoHandphone = S(r, "no_handphone"),
        DokumenKk = S(r, "dokumen_kk"),
        DokumenAkta = S(r, "dokumen_akta"),
        DokumenKia = S(r, "dokumen_kia"),
        DokumenIjazah = S(r, "dokumen_ijazah"),
        Catatan = S(r, "catatan"),
        Status = Enum.Parse<StatusCalonSiswa>(S(r, "status")!),
        SiswaId = siswaBaru,
        CreatedAt = DT(r, "created_at"),
        UpdatedAt = DT(r, "updated_at"),
    });
}
await db.SaveChangesAsync();
laporan.Add(("calon_siswa", await HitungMysql("calon_siswa"), await db.CalonSiswa.CountAsync()));

// ================= 14. Ekskul =================
Console.WriteLine("Migrasi Ekskul...");
var eksRows = await BacaSemua("ekskul", "ekskul_id");
var petaEkskul = new Dictionary<int, int>();
foreach (var r in eksRows)
{
    var e = new Ekskul { Nama = S(r, "nama")!, Pembina = S(r, "pembina"), Hari = S(r, "hari"), Deskripsi = S(r, "deskripsi"), IsActive = B(r, "is_active"), ArchivedAt = DT(r, "archived_at"), CreatedAt = DT(r, "created_at"), UpdatedAt = DT(r, "updated_at") };
    db.Ekskul.Add(e);
    await db.SaveChangesAsync();
    petaEkskul[I(r, "ekskul_id")] = e.EkskulId;
}
laporan.Add(("ekskul", await HitungMysql("ekskul"), await db.Ekskul.CountAsync()));

// ================= 15. Ekskul Siswa =================
Console.WriteLine("Migrasi Ekskul Siswa...");
var esRows = await BacaSemua("ekskul_siswa", "id");
var dilewatiEs = 0;
foreach (var r in esRows)
{
    var eksLama = I(r, "ekskul_id");
    var siswaLama = I(r, "siswa_id");
    if (!petaEkskul.TryGetValue(eksLama, out var eksBaru) || !petaSiswa.TryGetValue(siswaLama, out var siswaBaru)) { dilewatiEs++; continue; }
    db.EkskulSiswa.Add(new EkskulSiswa { EkskulId = eksBaru, SiswaId = siswaBaru, TanggalDaftar = DO(r, "tanggal_daftar") ?? DateOnly.FromDateTime(DateTime.Today), CreatedAt = DT(r, "created_at") });
}
await db.SaveChangesAsync();
laporan.Add(("ekskul_siswa", await HitungMysql("ekskul_siswa"), await db.EkskulSiswa.CountAsync()));
if (dilewatiEs > 0) Console.WriteLine($"  PERINGATAN: {dilewatiEs} baris ekskul_siswa dilewati.");

// ================= 16. Jadwal Pelajaran =================
Console.WriteLine("Migrasi Jadwal Pelajaran (bisa lama, ribuan baris)...");
var jpRows = await BacaSemua("jadwal_pelajaran", "jadwal_pelajaran_id");
var dilewatiJp = 0;
foreach (var r in jpRows)
{
    var taLama = I(r, "tahun_ajaran_id");
    var kelasLama = I(r, "kelas_id");
    var jamLama = I(r, "jam_pelajaran_id");
    var mapelLama = I(r, "mata_pelajaran_id");
    var guruLama = IN(r, "guru_id");
    if (!petaTa.TryGetValue(taLama, out var taBaru) || !petaKelas.TryGetValue(kelasLama, out var kelasBaru) ||
        !petaJam.TryGetValue(jamLama, out var jamBaru) || !petaMapel.TryGetValue(mapelLama, out var mapelBaru))
    { dilewatiJp++; continue; }
    int? guruBaru = guruLama is not null && petaGuru.TryGetValue(guruLama.Value, out var gb) ? gb : null;
    db.JadwalPelajaran.Add(new JadwalPelajaran { TahunAjaranId = taBaru, Semester = Enum.Parse<Semester>(S(r, "semester")!), KelasId = kelasBaru, JamPelajaranId = jamBaru, MataPelajaranId = mapelBaru, GuruId = guruBaru, CreatedAt = DT(r, "created_at"), UpdatedAt = DT(r, "updated_at") });
}
await db.SaveChangesAsync();
laporan.Add(("jadwal_pelajaran", await HitungMysql("jadwal_pelajaran"), await db.JadwalPelajaran.CountAsync()));
if (dilewatiJp > 0) Console.WriteLine($"  PERINGATAN: {dilewatiJp} baris jadwal_pelajaran dilewati.");

// ================= 17. Jadwal Piket =================
Console.WriteLine("Migrasi Jadwal Piket...");
var jpiRows = await BacaSemua("jadwal_piket", "jadwal_piket_id");
var dilewatiJpi = 0;
foreach (var r in jpiRows)
{
    var taLama = I(r, "tahun_ajaran_id");
    var guruLama = I(r, "guru_id");
    if (!petaTa.TryGetValue(taLama, out var taBaru) || !petaGuru.TryGetValue(guruLama, out var guruBaru)) { dilewatiJpi++; continue; }
    db.JadwalPiket.Add(new JadwalPiket { TahunAjaranId = taBaru, Semester = Enum.Parse<Semester>(S(r, "semester")!), Hari = Enum.Parse<Hari>(S(r, "hari")!), GuruId = guruBaru, CreatedAt = DT(r, "created_at"), UpdatedAt = DT(r, "updated_at") });
}
await db.SaveChangesAsync();
laporan.Add(("jadwal_piket", await HitungMysql("jadwal_piket"), await db.JadwalPiket.CountAsync()));
if (dilewatiJpi > 0) Console.WriteLine($"  PERINGATAN: {dilewatiJpi} baris jadwal_piket dilewati.");

// ================= 18. Kalender Akademik =================
Console.WriteLine("Migrasi Kalender Akademik...");
var kalRows = await BacaSemua("kalender_akademik", "kalender_akademik_id");
var dilewatiKal = 0;
foreach (var r in kalRows)
{
    var taLama = I(r, "tahun_ajaran_id");
    if (!petaTa.TryGetValue(taLama, out var taBaru)) { dilewatiKal++; continue; }
    db.KalenderAkademik.Add(new KalenderAkademik
    {
        TahunAjaranId = taBaru,
        Judul = S(r, "judul")!,
        Kategori = S(r, "kategori"),
        Warna = S(r, "warna"),
        TanggalMulai = DO(r, "tanggal_mulai")!.Value,
        TanggalSelesai = DO(r, "tanggal_selesai"),
        Waktu = S(r, "waktu"),
        Sasaran = S(r, "sasaran"),
        IsLibur = B(r, "is_libur"),
        Keterangan = S(r, "keterangan"),
        CreatedAt = DT(r, "created_at"),
        UpdatedAt = DT(r, "updated_at"),
    });
}
await db.SaveChangesAsync();
laporan.Add(("kalender_akademik", await HitungMysql("kalender_akademik"), await db.KalenderAkademik.CountAsync()));
if (dilewatiKal > 0) Console.WriteLine($"  PERINGATAN: {dilewatiKal} baris kalender_akademik dilewati.");

// ================= 19. Riwayat Akademik =================
Console.WriteLine("Migrasi Riwayat Akademik...");
var raRows = await BacaSemua("riwayat_akademik", "riwayat_akademik_id");
var dilewatiRa = 0;
foreach (var r in raRows)
{
    var siswaLama = I(r, "siswa_id");
    var taLama = I(r, "tahun_ajaran_id");
    if (!petaSiswa.TryGetValue(siswaLama, out var siswaBaru) || !petaTa.TryGetValue(taLama, out var taBaru)) { dilewatiRa++; continue; }
    // kelas_id PHP lama bisa literal 0 (siswa tanpa kelas saat diluluskan) - itu
    // BUKAN FK sungguhan di PHP asli, dan C# sengaja pakai null bukan 0 (lihat
    // komentar entity RiwayatAkademik.cs) - petakan 0/tidak-ketemu jadi null,
    // JANGAN simpan 0 mentah ataupun lewati barisnya.
    var kelasLama = IN(r, "kelas_id");
    int? kelasBaru = kelasLama is > 0 && petaKelas.TryGetValue(kelasLama.Value, out var kb2) ? kb2 : null;
    db.RiwayatAkademik.Add(new RiwayatAkademik { SiswaId = siswaBaru, TahunAjaranId = taBaru, KelasId = kelasBaru, Status = Enum.Parse<StatusRiwayatAkademik>(S(r, "status")!), CreatedAt = DT(r, "created_at"), UpdatedAt = DT(r, "updated_at") });
}
await db.SaveChangesAsync();
laporan.Add(("riwayat_akademik", await HitungMysql("riwayat_akademik"), await db.RiwayatAkademik.CountAsync()));
if (dilewatiRa > 0) Console.WriteLine($"  PERINGATAN: {dilewatiRa} baris riwayat_akademik dilewati.");

// ================= Laporan akhir =================
Console.WriteLine();
Console.WriteLine("=== LAPORAN HASIL MIGRASI ===");
Console.WriteLine($"{"Tabel",-24} {"MySQL",8} {"SQLite",8}  Status");
var semuaCocok = true;
foreach (var (tabel, sumber, tujuan) in laporan)
{
    // Kolom "SQLite" dibandingkan ke jumlah baris SUMBER dikurangi yang sengaja
    // dilewati (dicatat di peringatan atas) - beda jumlah selain itu = bug.
    var status = tujuan <= sumber ? (tujuan == sumber ? "OK" : "ada baris dilewati (lihat peringatan)") : "TIDAK NORMAL (>sumber?!)";
    if (tujuan != sumber) semuaCocok = false;
    Console.WriteLine($"{tabel,-24} {sumber,8} {tujuan,8}  {status}");
}
Console.WriteLine();
if (semuaCocok)
{
    Console.WriteLine("SEMUA TABEL COCOK 100% (jumlah baris SQLite = MySQL).");
}
else
{
    Console.WriteLine("ADA baris yang dilewati - PERIKSA peringatan di atas satu per satu sebelum dipakai produksi.");
}
Console.WriteLine();
Console.WriteLine("LANGKAH SELANJUTNYA (WAJIB, tidak otomatis oleh alat ini):");
Console.WriteLine("1. Salin folder dokumen upload PHP (writable/uploads/dokumen atau serupa) ke");
Console.WriteLine("   App_Data/uploads/dokumen di instalasi desktop - nama file di kolom Dokumen* harus cocok.");
Console.WriteLine("2. Jalankan aplikasi desktop, akan muncul halaman Setup Awal (tabel Users sengaja");
Console.WriteLine("   TIDAK dimigrasi) - buat akun admin baru dengan password baru.");
Console.WriteLine("3. Cek menu Kepala Sekolah, Guru Pengampu, dan Wali Kelas satu-persatu di UI -");
Console.WriteLine("   data relasinya sudah dipindah tapi patut dicek visual sebelum go-live sungguhan.");

return 0;

static string? GetArg(string[] a, string name)
{
    for (var i = 0; i < a.Length - 1; i++)
        if (a[i] == name) return a[i + 1];
    return null;
}

static string MaskPassword(string connStr) =>
    System.Text.RegularExpressions.Regex.Replace(connStr, @"(Password=)[^;]*", "$1***", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
