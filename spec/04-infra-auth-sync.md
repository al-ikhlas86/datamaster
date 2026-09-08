# INVENTARISASI: Infrastruktur / Lintas-Modul — Auth, Dashboard, DevPreview, Sinkronisasi & Backup

Semua path absolut berbasis `C:\xampp\htdocs\webarsipdata-master`.

## 1. AUTENTIKASI & MANAJEMEN PENGGUNA

### 1.1 Skema tabel (Myth Auth, MySQL — lihat `vendor/myth/auth/src/Database/Migrations/2017-11-20-223112_create_auth_tables.php`, plus migrasi lokal `app/Database/Migrations/2026-05-03-000002_AddUserImageToUsers.php` yang menambah kolom `user_image` ke `users`)

- **users**: `id` PK, `email` (unique), `username` (unique, nullable), `password_hash`, `reset_hash`, `reset_at`, `reset_expires`, `activate_hash`, `status`, `status_message`, `active` (tinyint), `force_pass_reset`, `created_at`, `updated_at`, `deleted_at`, + `user_image` (varchar, default kemungkinan `'default.png'`).
- **auth_logins**: log percobaan login (ip_address, email, user_id, date, success) — TIDAK dihapus saat user dihapus (untuk audit).
- **auth_tokens**: token "remember me" (selector/hashedValidator pattern ala paragonie).
- **auth_reset_attempts**, **auth_activation_attempts**: rate-limit bawaan Myth Auth (bukan yang dipakai LoginThrottle custom — beda mekanisme).
- **auth_groups**: `id`, `name`, `description` — **TIDAK ADA unique index pada `name`** (cuma PK di id). Ini pernah menyebabkan bug nyata (lihat 1.4).
- **auth_permissions**: `id`, `name`, `description`.
- **auth_groups_permissions**, **auth_groups_users**, **auth_users_permissions**: tabel pivot many-to-many.

Grup yang benar-benar dipakai di sistem ini: **hanya `admin`**. Tidak ada grup lain yang terlihat dipakai di kode (Filters.php cuma referensi `role:admin`). Tidak ada tabel/menu "Manajemen Pengguna" berbasis web sama sekali — pembuatan akun, assignment grup, dan reset semuanya lewat CLI/`.bat`.

### 1.2 Alur login (PERSIS)

- Rute `/login`, `/logout`, `/register`, dll didaftarkan **otomatis oleh package vendor `myth/auth`** (bukan di `app/Config/Routes.php` milik aplikasi) — konsekuensinya, satu-satunya cara menambah perilaku custom ke proses login adalah lewat **filter global** (`app/Filters/LoginThrottleFilter.php`), bukan edit controller/route.
- Field login: `login` (menerima **email ATAU username** — dua `validFields` di `Myth\Auth\Config\Auth`: `['email', 'username']`) + `password`. View `app/Views/auth/login.php` menampilkan text input generik "emailOrUsername" karena `$config->validFields !== ['email']`.
- Checkbox **"Ingat login (30 hari)"** — `allowRemembering = true`, `rememberLength = 30 * DAY` (di `app/Config/Auth.php`, override dari default `false`/30 hari vendor).
- **`allowRegistration = false`** — dimatikan sengaja. Komentar eksplisit: *"Dimatikan (temuan audit keamanan): sebelum RBAC aktif, siapapun yang daftar sendiri otomatis punya akses PENUH ke data siswa/guru/kelas - tidak ada grup/permission apapun yang membedakan. Akun baru sekarang HANYA dibuat manual oleh admin (`php spark auth:create_user`), lalu di-assign ke grup admin manual juga (`auth:add_to_group`)."* Halaman `/register` (`app/Views/auth/register.php`) masih ada di kode tapi TIDAK bisa diakses (guard di level Myth Auth), dan link "Belum Punya Akun?" di halaman login hanya muncul kalau `$config->allowRegistration` true (sekarang selalu disembunyikan).
- **`requireActivation = null`** — akun langsung aktif, tidak ada email konfirmasi (email server bahkan mungkin tidak dikonfigurasi di lokasi sekolah).
- **`silent = true`** — akses ditolak RBAC = redirect halus + pesan "tidak cukup hak akses", BUKAN exception mentah/stack trace.
- Hash password: **`Myth\Auth\Password::hash()` / `::verify()`** (wrapper Myth Auth di atas `password_hash()`/`password_verify()` PHP native, algoritma `PASSWORD_DEFAULT`, cost 10 — lihat `hashAlgorithm`/`hashCost` di `vendor/myth/auth/src/Config/Auth.php`, tidak di-override lokal).
- Password minimal 8 karakter (`minimumPasswordLength = 8`, dicek juga manual di `User::updatePassword()`).

### 1.3 RBAC (Role-Based Access Control)

Filter `role:admin` (`Myth\Auth\Filters\RoleFilter`) dipasang di `app/Config/Filters.php` untuk rute: `siswa`, `siswa/*`, `calon-siswa`, `calon-siswa/*`, `guru`, `guru/*`, `kelas`, `kelas/*`, `tahun-ajaran`, `tahun-ajaran/*`, `akademik`, `akademik/*`, `user`, `user/*`. Filter `login` (cek sudah login saja, tanpa cek grup) dipasang terpisah untuk: `dashboard`, `dashboard/*`, `siswa`, `siswa/*`, `kelas`, `kelas/*`, `user`, `user/*` — sengaja redundan dengan `role:admin` ("defense in depth", RoleFilter sendiri sudah redirect ke `/login` kalau belum login).

**Perilaku halus penting**: akun baru yang dibuat via `auth:create_user` TIDAK otomatis masuk grup `admin`. Kalau lupa `auth:add_to_group`, user bisa login sukses tapi setiap klik menu (Tambah Siswa, Data Guru, dll) selalu di-redirect balik ke Dashboard tanpa pesan error jelas — ini sebabnya ada `PERBAIKI_AKUN_ADMIN.bat` (lihat 1.4).

### 1.4 Rate limiting login (custom, di luar Myth Auth)

`App\Libraries\LoginThrottle` (dipakai lewat `App\Filters\LoginThrottleFilter`, terpasang di `Filters.php` sebagai filter global khusus rute `login` — `before` DAN `after`):
- **BUKAN lockout permanen** — sengaja, karena lockout permanen bisa disalahgunakan untuk mengunci akun ORANG LAIN cukup dengan salah password berkali-kali.
- Backoff bertahap berbasis cache (key = `login_throttle_` + md5(login value), TTL 3600 detik):
  - ≥3 kali gagal → tunggu **30 detik**
  - ≥5 kali gagal → tunggu **60 detik**
  - ≥7 kali gagal → tunggu **300 detik (5 menit)**
- Direset otomatis begitu login berhasil (`reset()` dipanggil di filter `after()` kalau `logged_in()` true setelah `attemptLogin()` vendor selesai jalan).
- `before()`: hanya menahan method POST (bukan tampilan GET form login).
- Catatan implementasi kritis: filter ini **tidak** extends `Myth\Auth\Filters\BaseFilter`, jadi helper `auth` di-load manual (`helper('auth')`) di `after()` — kalau lupa, `logged_in()` tidak terdefinisi.

### 1.5 Halaman "Setting" (`app/Controllers/User.php`) — pengganti Edit Profil + Logout terpisah

Digabung jadi SATU halaman sejak 2026-08-27 (komentar eksplisit di kode). Route group `user` (`filter: login`, plus `role:admin` di Filters.php — lihat 1.3):
- `GET /user` → `User::index()` — halaman utama.
- `POST /user/update` → `User::update()` — ubah username + foto profil.
- `POST /user/password` → `User::updatePassword()` — ubah password sendiri.
- `POST /user/backup-manual` → `User::backupManual()`.
- `POST /user/jadwal-backup` → `User::simpanJadwalBackup()`.
- `POST /user/restore` → `User::restore()`.

**Form/field di halaman Setting** (`app/Views/user/index.php`):
1. **Kartu Profil**: foto (`user_image`, klik foto = trigger file input, preview via FileReader JS sebelum submit), username (readonly-nya email), badge status Aktif/Tidak Aktif. Validasi upload: JPG/PNG, maks 500KB (`max_size[user_image,500]`, `is_image`, `ext_in`, `mime_in`), dicek client-side juga.
   - Validasi username: `required|min_length[3]|max_length[30]|alpha_numeric_space|is_unique[users.username,id,{id}]`.
   - Update foto: file lama dihapus (`unlink`) kecuali `default.png`, disimpan ke `FCPATH.'img/'` dengan nama random (`getRandomName()`).
2. **Kartu Ubah Password**: `password_lama` (wajib cocok — diverifikasi via `Myth\Auth\Password::verify()` terhadap `password_hash` di DB SEBELUM boleh ganti, mencegah orang yang sesi-nya belum logout mengganti password tanpa tahu yang lama), `password_baru` (min 8), `password_baru_konfirmasi` (`matches[password_baru]`). Fitur ini **dulu TIDAK ADA sama sekali** di web (hanya bisa via script `PERBAIKI_AKUN_ADMIN.bat`).
3. **Kartu Backup & Pemulihan**:
   - Kartu info "Backup Manual Terakhir" & "Backup Online Terakhir" (dari `DatabaseBackup::waktuTerakhir('last_backup_manual_at'/'last_backup_online_at')`, format `d M Y H:i`).
   - Form "Jam Backup Online Otomatis" — dropdown 00–23, submit ke `jadwal-backup`.
   - Tombol "Backup Manual Sekarang" (dgn konfirmasi SweetAlert, submit ke `backup-manual`).
   - Tombol "Pemulihan Data" (buka modal).
   - **Modal Pemulihan**: upload file `.sql` atau `.sql.enc`, field `sandi_restore` (muncul kondisional via JS kalau ekstensi `.enc`), field `konfirmasi_restore` wajib diketik **"TIMPA"** persis (tombol submit disabled sampai teks cocok persis, case-insensitive di JS tapi server pakai `strtoupper(trim())`).
4. **Kartu Logout**: link `base_url('logout')`.

### 1.6 Detail method `User.php`

- **`backupManual()`**: dump via `DatabaseBackup::dump()` (mysqldump, TANPA enkripsi, TANPA internet), simpan ke `ROOTPATH.'backup/'` dengan nama `manual_YYYY-MM-DD_His.sql`. **Rotasi: simpan maksimal 7 backup manual TERBARU** (bug nyata ditemukan 2026-08-28: sebelumnya rotasi cuma ada di backup harian `START_WEBARSIPDATA.bat`, tombol manual ini luput sama sekali dari rotasi — sekarang diperbaiki via `glob()` + `rsort()` + hapus sisa selain 7 pertama). Catat waktu via `DatabaseBackup::catatWaktu('last_backup_manual_at')`.
- **`simpanJadwalBackup()`**: validasi `jam_backup` harus digit 0–23 via `ctype_digit`, simpan via `DatabaseBackup::simpanJamBackupOnline()`. Konteks: sebelum 2026-09-02 jam backup online dipaku hardcode jam 12:00 di Task Scheduler; sekarang bisa diatur admin dari sini dan `backup:cloud` sendiri yang memutuskan waktunya (task Windows cukup jalan tiap 30 menit).
- **`restore()`**: terima `.sql` polos atau `.sql.enc` terenkripsi. Wajib ketik "TIMPA" persis. Kalau `.enc`, wajib `sandi_restore` diisi, dibuka via `DatabaseBackup::bukaBerkasEnkripsi()`. Setelah `terapkanSql()` sukses, **migrasi database dijalankan OTOMATIS** (`php spark migrate` via `exec()`, path php.exe dicoba `C:\xampp\php\php.exe` dulu baru fallback `php`) — ditegaskan ini BUKAN "auto-restore" (yang dilarang), hanya menaikkan struktur setelah restore data manual oleh manusia. Kalau migrasi gagal, pesan peringatan ditampilkan dan user diminta jalankan manual.

## 2. DASHBOARD (`app/Controllers/Dashboard.php`)

### 2.1 Percabangan tampilan

`index()` cek `effective_install_type()`: kalau `'perusahaan'` → panggil `indexPerusahaan()` (private). Komentar: *"Instalasi perusahaan tidak punya konsep siswa/kelas/tahun ajaran sama sekali - dashboard-nya harus 100% bersih dari data itu, bukan cuma menu sidebar yang disembunyikan."*

### 2.2 Dashboard Pendidikan — rumus/query PERSIS tiap statistik

| Variabel view | Rumus/Query |
|---|---|
| `totalSiswa` | `SiswaModel->where('status','aktif')->countAllResults()` |
| `totalL` | `SiswaModel->where('jenis_kelamin','L')->where('status','aktif')->countAllResults()` |
| `totalP` | `SiswaModel->where('jenis_kelamin','P')->where('status','aktif')->countAllResults()` |
| `totalAlumni` | `SiswaModel->where('status','lulus')->countAllResults()` |
| `recentSiswa` | `SiswaModel->where('status','aktif')->orderBy('created_at','DESC')->limit(5)->findAll()` |
| `tahunAktif` | `TahunAjaranModel->getActive()` |
| `chartLabels`/`chartData` | Dari `KelasModel->getKelasWithCount()`, filter `total_siswa > 0`, label = `nama_kelas`. **CATATAN: variabel ini dihitung tapi TIDAK DIPAKAI di `dashboard/index.php`** (view hanya render chart per-tingkat & doughnut gender) — kemungkinan dead code / chart per-kelas yang direncanakan tapi belum dipasang di UI. Perlu diputuskan apakah ini fitur yang hilang atau memang sengaja dibuang. |
| `perTingkatLabel`/`perTingkatData` | Akumulasi `chartLabels`/`chartData` di atas per string `"Kelas {tingkat}"` (bar chart "Jumlah Siswa per Tingkat" — ini yang benar-benar tampil) |

**Kartu statistik di view** (`app/Views/dashboard/index.php`): "Siswa Aktif" (`totalSiswa`), "Laki-laki" (`totalL`), "Perempuan" (`totalP`), "Alumni (Lulus)" (`totalAlumni`). Chart: Bar "Jumlah Siswa per Tingkat" (perTingkat), Doughnut "Distribusi Jenis Kelamin" (totalL vs totalP, dengan tooltip persentase). Tabel "Siswa Terbaru Ditambahkan" (5 baris dari `recentSiswa`: No, NIS, Nama, JK, Asal Sekolah, tombol Detail).

Jika `tahunAktif` tidak kosong, muncul alert hijau "Tahun Ajaran Aktif: {nama}".

### 2.3 Panel "Langkah Persiapan Awal" (`langkahPersiapan()`, private)

Muncul HANYA jika belum semua langkah selesai, hilang otomatis begitu semua tercentang. Urutan berjenjang (harus dikerjakan dari atas), 8 langkah, tiap langkah query PERSIS:

1. **Tahun ajaran aktif**: `ok = ($tahunAjaranId > 0)` dari `$tahunAktif['tahun_ajaran_id']`. Link: `tahun-ajaran`.
2. **Tingkat (Kelas 1-6)**: `hitung('tingkat', ['is_active'=>1]) > 0`. Link: `kurikulum?tab=tingkat`.
3. **Mata pelajaran + kode huruf**: `ok = (jmlMapel>0 && jmlKode>0)`, dimana `jmlKode = mata_pelajaran WHERE kode IS NOT NULL AND kode != ''`. Link: `kurikulum?tab=mapel`.
4. **Jam belajar harian**: `hitung('jam_pelajaran', ['tahun_ajaran_id'=>$taId]) > 0` (0 kalau belum ada tahun ajaran aktif). Link: `kurikulum?tab=jam`.
5. **Kelas (1A, 1B, dst)**: `hitung('kelas', ['is_active'=>1]) > 0`. Link: `kelas`.
6. **Guru + nomor urut**: `ok = (jmlGuru>0 && jmlNomor>0)`, `jmlGuru = guru WHERE status_aktif=1`, `jmlNomor = guru WHERE status_aktif=1 AND nomor_urut IS NOT NULL`. Link: `penugasan-mengajar`.
7. **Wali kelas**: `ok = (jmlKelas>0 && jmlWali >= jmlKelas)`, dimana `jmlWali = SELECT COUNT(DISTINCT w.kelas_id) FROM wali_kelas w JOIN kelas k ON k.kelas_id=w.kelas_id WHERE k.is_active=1` (DISTINCT supaya kelas dengan 2 wali tidak dihitung 2x — sejak kolom teks `kelas.wali_kelas` dibuang 2026-08-27, 1 kelas boleh punya sampai 2 wali via `WaliKelasModel`). Link: `kelas`.
8. **Jadwal pelajaran**: `hitung('jadwal_pelajaran', ['tahun_ajaran_id'=>$taId]) > 0`. Link: `jadwal-pelajaran`.

Tiap langkah punya `nama`, `ok` (bool), `url`, `ket` (keterangan kontekstual). `selesai` = true hanya jika SEMUA `ok`. UI: progress bar persentase (`sudah/jml*100`), list bernomor dengan icon check/circle, tombol "Lihat"/"Isi sekarang".

### 2.4 Dashboard Perusahaan (`indexPerusahaan()`, private)

Murni data pegawai, **tidak ada satu pun rujukan ke siswa/kelas/tahun ajaran**:
- `$pegawai = GuruModel->where('jabatan','karyawan')->where('status_aktif',1)->orderBy('nama','ASC')->findAll()`.
- `totalPegawai = count($pegawai)`.
- `totalL = count(array_filter($pegawai, fn($p) => $p['jenis_kelamin']==='L'))`; sama untuk `totalP`.
- `recentPegawai = GuruModel->where('jabatan','karyawan')->where('status_aktif',1)->orderBy('created_at','DESC')->limit(5)->findAll()`.
- View `dashboard/index_perusahaan.php`: 3 kartu (Total Pegawai Aktif, Laki-laki, Perempuan), doughnut gender, tabel "Pegawai Terbaru Ditambahkan" (No, Nama, JK, No. HP, Detail → `guru/{id}`). Tidak ada panel langkah persiapan.

## 3. DevPreview (`app/Controllers/DevPreview.php`)

- Extends `CodeIgniter\Controller` langsung (BUKAN `BaseController`).
- Satu method: `set()`. Route: `POST /dev-preview/set` (filter `login`).
- **Guard server-side**: `if (install_type() !== 'pengembang') redirect ke /dashboard dengan error` — dicek ulang di server, TIDAK hanya disembunyikan di UI, sehingga instalasi pendidikan/perusahaan asli tidak pernah bisa mengakses fitur ini meski request langsung dikirim ke endpoint.
- Terima POST field `type`, harus salah satu dari `['pendidikan', 'perusahaan']` (in_array strict) — `'pengembang'` sendiri BUKAN pilihan tampilan (bukan tampilan tersendiri, cuma status yang membuka izin ganti-ganti tampilan).
- Simpan ke `session()->set('dev_preview_type', $type)`.
- Dipakai lewat dropdown `<select name="type" onchange="this.form.submit()">` di `app/Views/templates/sidebar.php`, HANYA dirender kalau `install_type() === 'pengembang'` (baris 27), dengan opsi dipilih dari `effective_install_type()`.
- Tujuan: developer bisa lihat persis tampilan Pendidikan/Perusahaan (termasuk menu mana yang ke-blokir) sebelum sistem di-launch, tanpa perlu instalasi terpisah.

## 4. Home.php & BaseController.php

- **`Home.php`**: `index()` — kalau `logged_in()` redirect ke `dashboard`, kalau tidak redirect ke `login`. Tidak ada view sendiri.
- **`BaseController.php`**: abstract, semua controller extends ini.
  - `initController()`: load helpers global `['form', 'url', 'auth']` — helper `auth` (dari Myth Auth) inilah yang menyediakan fungsi global `user()`, `logged_in()`, dll ke SEMUA controller.
  - Dua konstanta regex publik dipakai lintas modul (form Siswa/Guru dll — bukan auth, tapi didefinisikan di sini karena dipakai lintas controller):
    - `REGEX_NAMA = "/^[\p{L}\s'.,\-]+$/u"` — pola nama orang, WAJIB terima titik & koma untuk gelar akademik ("Suhada Akum, S.Pd.I"); bug ditemukan 2026-08-26: aturan lama menolak SEMUA 45 nama guru di daftar resmi sekolah.
    - `REGEX_TEKS_PENDEK = "/^[\p{L}\p{N}\s'.,\/\-]+$/u"` — pola teks pendek non-nama (pekerjaan, tempat lahir, kelurahan, kecamatan), lebih longgar (boleh angka & garis miring: "BUMN / BUMD", "RW 03", "D.I. Yogyakarta").

## 5. `app/Common.php` — helper global

```php
install_type(): string
```
Baca `env('install_type', 'pendidikan')`, lowercase+trim, harus salah satu dari `['pendidikan','perusahaan','pengembang']` else fallback `'pendidikan'`. Default `'pendidikan'` supaya instalasi lama (sebelum fitur ini ada) tetap jalan tanpa perubahan.

```php
effective_install_type(): string
```
Kalau `install_type() !== 'pengembang'` → return `install_type()` apa adanya (tidak bisa diubah instalasi asli). Kalau `'pengembang'` → baca `session('dev_preview_type')`, valid hanya `pendidikan`/`perusahaan`, default `pendidikan` kalau session kosong/tidak valid. **Tidak pernah mengembalikan `'pengembang'`** — itu bukan tampilan, cuma status izin.

```php
is_pendidikan_module_active(): bool
```
`= (effective_install_type() === 'pendidikan')`. Dipakai di sidebar (sembunyikan menu) dan `PendidikanFilter` (blokir server-side).

## 6. Konfigurasi Myth Auth (`app/Config/Auth.php`, extends `Myth\Auth\Config\Auth`)

Override dari default vendor:
- `allowRemembering = true` (default `false`), `rememberLength = 30 * DAY`.
- `allowRegistration = false` (default `true`).
- `silent = true` (default `false`).
- `views`: override `login` → `auth/login` (custom themed), `register` → `auth/register` (custom, tapi tak terjangkau karena registration off); sisanya (`forgot`, `reset`, email templates) masih pakai default vendor `Myth\Auth\Views\*`.
- `requireActivation = null` (default `'Myth\Auth\Authentication\Activators\EmailActivator'`).

Tidak di-override (masih default vendor, relevan untuk clone):
- `validFields = ['email', 'username']`.
- `activeResetter = 'Myth\Auth\Authentication\Resetters\EmailResetter'` — artinya fitur "Lupa Password" via email MASIH aktif (link muncul di halaman login), meski kemungkinan besar SMTP tidak dikonfigurasi di instalasi sekolah nyata — perlu diverifikasi apakah ini benar dipakai atau efektif mati karena email gagal terkirim.
- `hashAlgorithm = PASSWORD_DEFAULT`, `hashCost = 10`, `minimumPasswordLength = 8`.
- `passwordValidators`: `CompositionValidator`, `NothingPersonalValidator`, `DictionaryValidator` (PwnedValidator dikomentari/nonaktif) — ini berlaku untuk *registrasi/reset password via Myth Auth*, TAPI **`User::updatePassword()` custom TIDAK memanggil validator-validator ini sama sekali** — hanya `min_length[8]` dan `matches` — artinya password lemah (mis. sama dengan username) BISA lolos lewat halaman Setting meski tidak akan lolos lewat alur reset password resmi Myth Auth. Ini inkonsistensi yang perlu diputuskan saat re-write: apakah re-implementasi C# perlu menyamakan validator composition juga di endpoint ganti password sendiri.
- `defaultUserGroup`, `landingRoute` — default vendor (tidak terlihat dipakai karena registrasi mati & routing dashboard eksplisit).

## 7. `app/Commands/SyncPush.php` — SINKRONISASI KE HUB API (PALING KRITIS)

### 7.1 Autentikasi & konfigurasi
- `hub_api.url` dan `hub_api.token` dibaca dari `.env` (`env('hub_api.url','')`, `env('hub_api.token','')`, di-trim, URL di-`rtrim('/')`).
- Kalau salah satu kosong → command berhenti dengan pesan error, TIDAK crash.
- Auth ke Hub API: header `Authorization: Bearer {token}` di setiap request (push & pull).
- **Instalasi teridentifikasi murni lewat token** — tidak ada field "unit_id" yang dikirim di payload manapun. (Catatan: ada juga variabel `unit` yang disimpan ke `.env` oleh `konfigurasi_hub_api.ps1`/`KONFIGURASI_HUB_API.bat`, tapi **tidak pernah dibaca oleh PHP manapun** — grep membuktikan hanya `hub_api.url` dan `hub_api.token` yang dipakai kode. Kemungkinan `unit` di `.env` murni untuk keperluan dokumentasi/manual admin saat membuat token di sisi Hub API, bukan dikonsumsi aplikasi ini.)

### 7.2 Urutan pemanggilan (di `run()`)

Jika `install_type() !== 'perusahaan'` (pendidikan ATAU pengembang):
1. `pushKelas`
2. `pushTahunAjaran`
3. `pushSiswa`
4. `pushMataPelajaran`
5. `pushJamPelajaran`

Selalu (semua tipe instalasi, tapi kalau perusahaan filter `jabatan='karyawan'`):

6. `pushGuru($companyOnly = install_type()==='perusahaan')`

Jika bukan perusahaan lagi, SETELAH guru (harus terakhir, karena bergantung guru & kelas sudah tersinkron):

7. `pushWaliKelas`
8. `pushKepalaSekolah`
9. `pushJadwalPelajaran`
10. `pushKalenderAkademik`
11. `pushCalonSiswa`
12. `pullKeputusanPsb`

Alasan urutan ini eksplisit di komentar: "Jadwal Pelajaran, Wali Kelas, Kepala Sekolah BUTUH guru & kelas SUDAH tersinkron dulu (Mobile-app resolve nama guru/kelas/mapel lewat source_id masing2, bukan dikirim ulang di sini) - makanya ditaruh PALING TERAKHIR dlm urutan push, walau efeknya baru kerasa di siklus tarik Mobile-app berikutnya (push & pull adalah 2 proses independen, bukan 1 transaksi)."

### 7.3 Tabel detail per entitas

| Entitas | Method | Endpoint | `full` flag | Field JSON dikirim | Sumber kolom lokal | Transformasi |
|---|---|---|---|---|---|---|
| Kelas | `pushKelas` | `POST /api/v1/sync/kelas` | false | `source_id, tingkat, tingkat_kode, nama, kelompok, is_active` | `kelas_id, tingkat, nama_kelas, kelompok, is_active` (semua `KelasModel->findAll()`) | `tingkat` = label dari `TingkatModel->getLabelMap()[kode]` atau fallback `TingkatModel::labelFallback()`; `tingkat_kode` = kode mentah as string; `kelompok` cast int atau null; `is_active` default 1 kalau null |
| Tahun Ajaran | `pushTahunAjaran` | `/api/v1/sync/tahun-ajaran` | false | `source_id, nama, is_active` | `tahun_ajaran_id, nama, is_active` | cast int |
| Siswa | `pushSiswa` | `/api/v1/sync/siswa` | false | `source_id, nama, nis, nisn, jenis_kelamin, kelas_source_id, hp_ortu, status` | `SiswaModel->getAllWithKelas()`: `siswa_id, nama, nis, nisn, jenis_kelamin, kelas_id, no_handphone, status` | dikirim SEMUA (aktif + arsip); `kelas_source_id` bisa null; `hp_ortu` ← `no_handphone` (rename) |
| Guru/Pegawai | `pushGuru` | `/api/v1/sync/guru` | false | `source_id, nama, nip, jenis_kelamin, jabatan, kelas_source_id, no_handphone, status_aktif, is_kepala_sekolah, status_keluar` | `GuruModel->findAll()` (filter `jabatan='karyawan'` kalau perusahaan) | `kelas_source_id` **TIDAK LAGI** dari kolom `guru.kelas_id` (dibuang migrasi 2026-08-27) — sekarang di-lookup dari `WaliKelasModel->findAll()` (map `guru_id => kelas_id`, karena `wali_kelas` punya UNIQUE(guru_id): 1 guru maks jadi wali di 1 kelas, sehingga bentuk kiriman "1 guru = 1 kelas" tetap valid meski sekarang 1 kelas boleh >1 wali); `is_kepala_sekolah` = bool dari lookup `KepalaSekolahModel->getSaatIni()['guru_id']` (maks 1 baris "saat ini"); `status_keluar` (2026-09-07, "Status Akun Alumni": purna_bakti/resign/diberhentikan) dipakai Webview-App membedakan pegawai yang layak dapat akses alumni terbatas vs yang diblokir total |
| Wali Kelas | `pushWaliKelas` | `/api/v1/sync/wali-kelas` | **true (full snapshot)** | `source_id, kelas_source_id, guru_source_id, tahun_ajaran_source_id, urutan` | `WaliKelasModel->findAll()`: `wali_kelas_id, kelas_id, guru_id, tahun_ajaran_id, urutan` | **Riwayat SEMUA tahun ajaran dikirim sekaligus** (bukan cuma tahun aktif) — Hub API/Mobile-app butuh tahu penetapan tahun lampau/depan; `full: true` WAJIB karena baris yang tidak ikut terkirim berarti benar-benar dihapus (guru dilepas dari wali kelas), bukan sebagian data yang kebetulan tak disertakan |
| Kepala Sekolah | `pushKepalaSekolah` | `/api/v1/sync/kepala-sekolah` | **true** | `source_id, guru_source_id, tahun_ajaran_source_id` | `KepalaSekolahModel->findAll()`: `kepala_sekolah_id, guru_id, tahun_ajaran_id` | riwayat semua tahun ajaran, pola sama seperti Wali Kelas |
| Mata Pelajaran | `pushMataPelajaran` | `/api/v1/sync/mata-pelajaran` | **true** | `source_id, nama, kode, kelompok` | `mata_pelajaran_id, nama, kode, kelompok` | `kode`/`kelompok` bisa null |
| Jam Pelajaran | `pushJamPelajaran` | `/api/v1/sync/jam-pelajaran` | **true** | `source_id, tahun_ajaran_source_id, hari, jam_ke, jam_mulai, jam_selesai, jenis, label` | `JamPelajaranModel->findAll()` | `jam_mulai`/`jam_selesai` dipotong ke `HH:MM` (`substr(...,0,5)`, buang detik) |
| Kalender Akademik | `pushKalenderAkademik` | `/api/v1/sync/kalender-akademik` | **true** | `source_id, tahun_ajaran_source_id, judul, kategori, warna, tanggal_mulai, tanggal_selesai, waktu, sasaran, is_libur, keterangan` | `KalenderAkademikModel->findAll()`, field 1:1 | `is_libur` cast int |
| Jadwal Pelajaran | `pushJadwalPelajaran` | `/api/v1/sync/jadwal-pelajaran` | **true** | `source_id, tahun_ajaran_source_id, semester, kelas_source_id, mata_pelajaran_source_id, guru_source_id, jam_pelajaran_source_id, jam_ke, hari, jam_mulai, jam_selesai` | JOIN raw SQL: `jadwal_pelajaran jp JOIN jam_pelajaran j ON j.jam_pelajaran_id=jp.jam_pelajaran_id`, select `jp.*, j.hari, j.jam_ke, j.jam_mulai, j.jam_selesai` | Jadwal sekarang menunjuk `jam_pelajaran` (jam ke-N) bukan simpan jam sendiri; `hari`/`jam` tetap dikirim (hasil join) supaya konsumen tak perlu tarik tabel jam terpisah; `guru_source_id` boleh null (mapel berkelompok tanpa guru tetap); `jam_mulai`/`selesai` dipotong `HH:MM` |
| PSB / Calon Siswa | `pushCalonSiswa` | `/api/v1/sync/psb` | false | `source_id, nama, jenis_kelamin, nisn, tempat_lahir, tanggal_lahir, asal_sekolah, alamat_jalan, alamat_kelurahan, alamat_kecamatan, nama_ayah, nama_ibu, no_handphone, dokumen_lengkap, status, catatan` | `CalonSiswaModel->findAll()`, field 1:1 kecuali `dokumen_lengkap` | Dikirim **SEMUA status** (bukan cuma 'menunggu') supaya perubahan status yang terjadi LOKAL (TU proses via web) ikut ter-update di Mobile-app; `dokumen_lengkap` = `1` HANYA jika `dokumen_kk && dokumen_akta && dokumen_kia && dokumen_ijazah` semua truthy, else `0`; **nama file dokumen fisik TIDAK PERNAH dikirim** (cuma ada di disk PC ini, tidak berguna di HP) |

Ditambah 1 arah pull:

| Arah | Method | Endpoint | Keterangan |
|---|---|---|---|
| Pull keputusan PSB | `pullKeputusanPsb` | `GET /api/v1/psb/keputusan` | Tarik antrean keputusan Terima/Tolak yang dititipkan dari HP (Mobile-app). Untuk tiap item: cek dulu apakah status lokal SUDAH sama persis dengan keputusan (idempotency guard — kalau ya, lapor `applied` tanpa proses ulang, BUKAN dianggap gagal). Kalau `keputusan==='terima'`: cari `KelasModel->find(kelas_source_id)` — kalau kelas tidak ada (dihapus/diubah), lapor `failed`; kalau ada, panggil `PsbProcessor->terima($calonSiswaId, $nis, $kelasId)` — **method processor yang SAMA PERSIS dengan jalur web** (lihat `PsbProcessor`, dicakup agent modul PSB). Kalau `keputusan==='tolak'`: `PsbProcessor->tolak($calonSiswaId, $catatan)`. Hasil dilaporkan balik ke Hub API. |
| Lapor balik | `laporKeputusanSelesai` | `POST /api/v1/psb/keputusan/{id}/selesai` | Body: `{status: 'applied'|'failed', pesan_gagal}`. **SENGAJA dijalankan SETELAH `pushCalonSiswa`** dalam urutan `run()` — kalau dibalik, keputusan yang baru diterapkan detik itu baru ter-push 1 siklus (1 menit) kemudian; bukan masalah besar tapi tidak ada alasan menunda. Kalau lapor balik GAGAL (exception, internet putus): keputusan tetap `pending` di Hub API, ditarik LAGI siklus berikutnya — idempotent by design karena `PsbProcessor` menolak proses ulang calon yang statusnya sudah bukan 'menunggu', jadi diproses dobel tidak menghasilkan siswa dobel, cuma dilaporkan "sudah diproses sebelumnya" dan ditandai failed di Hub API — BUKAN celah. |

### 7.4 Mekanisme fingerprint / hemat jaringan (method `post()`, private, dipakai semua push)

- Path fingerprint: `WRITEPATH.'sync_state/'.md5($path).'.hash'` — sengaja di `writable/` (bukan tabel DB) supaya **tidak ikut ter-backup/ter-restore** (kalau hilang, efeknya cuma 1 siklus push penuh lalu hemat lagi, bukan data rusak).
- **Kenapa penting** (angka nyata dari komentar kode, 2026-08-27): tanpa fingerprint, `sync:push` yang jalan tiap 1 menit (Task Scheduler) mengirim ULANG seluruh isi tabel setiap siklus walau tidak ada perubahan — terukur nyata: jadwal pelajaran saja 469 KB/siklus = ~659 MB/hari (~19 GB/bulan) upload dari PC TU sekolah. Hub API memang sudah bisa menjawab "0 berubah", tapi **biaya jaringan sudah terlanjur dibayar di sisi sekolah** sebelum sampai ke server.
- Perhitungan fingerprint: `hash('sha256', 'v4-full|' . json_encode($rows))` — **HARUS menyertakan versi PROTOKOL** (string `'v4-full|'`), bukan cuma isi data. Riwayat kenaikan versi (naikkan tiap kali BENTUK kiriman berubah):
  - Tanpa versi awalnya: payload identik antar-PC tapi bentuk kiriman sudah berubah tidak akan pernah terkirim ulang.
  - Saat dukungan hapus ditambahkan (2026-09-02): payload di banyak PC sudah terlanjur identik dengan kiriman terakhir — tanpa penanda versi, kiriman ber-`full` pertama tidak akan pernah terkirim, dan agenda yang sudah dihapus (contoh nyata di komentar: "Libur Mukhtamar") akan tetap "menghantui" HP sampai ada perubahan lain yang kebetulan terjadi.
  - **v3** (2026-09-04): payload guru bertambah `is_kepala_sekolah`.
  - **v4** (2026-09-07): payload guru bertambah `status_keluar`.
  - **Untuk clone C#: setiap kali bentuk payload entitas manapun berubah, string versi protokol ini WAJIB dinaikkan juga**, kalau tidak PC yang datanya kebetulan identik dengan kiriman versi lama tidak akan pernah mengirim ulang meski field baru sudah ada di kode.
- Alur cek: kalau `is_file($fpFile)` dan isinya (`trim(file_get_contents)`) sama persis dengan fingerprint baru → **skip kirim sepenuhnya** (log "(tidak berubah sejak sync terakhir - tidak dikirim)").
- Fingerprint BARU disimpan **HANYA setelah server menjawab sukses DAN `errorCount === 0`** (semua baris diterima tanpa error) — kalau kirim gagal (internet mati/server down) atau server menolak sebagian baris, fingerprint TIDAK disimpan, sehingga siklus berikutnya otomatis retry — "tidak ada perubahan yang bisa hilang gara-gara optimasi ini."

### 7.5 Perilaku tabel kosong vs snapshot (`empty($rows) && !$snapshotPenuh`)

- Entitas non-full (siswa, guru, kelas dll — siklus hidupnya lewat kolom status, tidak pernah benar-benar dihapus): kalau `$rows` kosong, TIDAK dikirim sama sekali (log "(tidak ada data)").
- Entitas `full=true` (kalender, jadwal, jam, mapel, wali-kelas, kepala-sekolah — barisnya BISA dihapus admin TU): tabel kosong TETAP dikirim, karena "kosong" itu sendiri sebuah kabar — Admin TU baru saja menghapus baris terakhir, konsumen perlu tahu supaya ikut mengosongkan.

### 7.6 Format request & response

- Request: `POST {url}{path}`, header `Authorization: Bearer {token}`, body JSON `{data: $rows, full: $snapshotPenuh}`, `http_errors=false`, timeout 15 detik (`service('curlrequest', ['timeout'=>15])`).
- Response sukses (HTTP 200 + `success` truthy): dibaca `synced`, `changed` (atau `synced` sbg fallback), `unchanged`, `deleted`, `errors[]` (array `{index, message}`). Log ringkas: `"Sync: N diterima; N berubah, N tetap[, N dihapus]; N error."` — warna kuning kalau ada error, hijau kalau bersih.
- Gagal koneksi (exception): log error, TIDAK menyimpan fingerprint, lanjut ke push berikutnya (tidak menghentikan seluruh `run()`).
- Gagal HTTP non-200 atau `success` falsy: log error dengan pesan dari `$body['message']` atau raw body.

### 7.7 Catatan `source_id` (komentar kode, penting untuk desain ulang)

*"source_id di tiap kolom \*_source_id di sini SELALU id ASLI baris terkait di WebArsipData (kelas_id/mata_pelajaran_id/guru_id/tahun_ajaran_id) - BUKAN id hasil sync Hub API - konsumen resolve nama sendiri via (unit_id, source_id) itu, pola sama persis dengan siswa.kelas_source_id."* Artinya di sisi Hub API, identitas unit + source_id lokal adalah kunci komposit untuk resolve relasi — bukan foreign key global. Ini krusial dipahami untuk desain ulang C# karena ID lokal SQLite per-instalasi harus tetap dikirim apa adanya, bukan di-mapping ke ID lain.

## 8. `app/Commands/BackupCloud.php` & `BackupRestore.php` — BACKUP TERENKRIPSI

### 8.1 Alasan (komentar di kode)
Sebelum 2026-08-27, backup Data Master HANYA tersimpan di folder `backup/` pada disk yang SAMA dengan databasenya — disk mati = data DAN backup hilang bersamaan, jadi sebenarnya tidak terlindungi dari skenario paling mungkin terjadi di PC sekolah.

### 8.2 Alur `backup:cloud` (`run()`)
1. Validasi `hub_api.url`/`hub_api.token` terisi (kalau tidak, berhenti).
2. Validasi `backup.passphrase` (dari `.env`) **minimal 16 karakter** — SENGAJA menolak jalan (bukan generate kunci acak sendiri diam-diam): kalau kunci dibuat otomatis dan hanya tersimpan di PC ini lalu PC mati, backup ikut tidak bisa dibuka SELAMANYA — itu kegagalan lebih parah daripada tidak punya backup sama sekali, karena mengira terlindungi padahal tidak. Kalau kurang dari 16 karakter, tampilkan saran nilai acak (`bin2hex(random_bytes(24))`) tapi TIDAK dipakai otomatis.
3. `buatBackupHariIni($sandi, $dir)`: cek jam (`DatabaseBackup::jamBackupOnline()`, diatur Admin di Setting, default 12) — kalau jam sekarang (`date('G')`) kurang dari jam terjadwal, skip. Cek apakah sudah ada file `datamaster_{tanggal}_*.sql.enc` untuk hari ini di antrian — kalau sudah, skip (idempotent, aman dijalankan sesering apa pun). Dump via `DatabaseBackup::dump()`. Enkripsi: **AES-256-CBC**, kunci turunan **PBKDF2-SHA256, 100.000 iterasi**, salt 16 byte acak + IV 16 byte acak (per file). Format file: `"ARSIPV1" . $garam(16) . $iv(16) . $ciphertext`. Nama file: `datamaster_YYYY-MM-DD_His.sql.enc`, disimpan di `WRITEPATH.'backup-antrian/'`.
4. `kirimAntrian($url, $token, $dir)`: **jalan TANPA syarat jam** (beda dari langkah 3) — berkas yang mengendap karena internet mati harus bisa menyusul kapan pun. Kirim SEMUA file `*.sql.enc` di antrian, urut file terlama dulu (`sort()`). Upload via `POST {url}/api/v1/backup/upload`, `multipart: {berkas: CURLFile(...)}`, header Bearer token, timeout 120 detik. **Catatan implementasi kritis**: CI4 `CURLRequest` butuh `multipart` sebagai peta field=>nilai dengan file dibungkus `CURLFile` — BUKAN format array-of-parts ala Guzzle (kalau salah, terkirim sebagai field biasa dan ditolak HTTP 400 oleh server). Sukses (HTTP 200 + `success`): hapus file lokal (`unlink`), catat waktu (`catatWaktu('last_backup_online_at')`). Gagal exception (internet mati): file dibiarkan di antrian, `return` (stop loop, coba lagi siklus berikutnya). Ditolak server non-exception (mis. token salah): `return` juga (berhenti, tidak membanjiri server dengan percobaan file berikutnya).
5. `rotasiAntrian($dir)`: batas antrian **`ANTRIAN_MAKS = 7`** file — lebih dari itu, file TERLAMA (`sort()` lalu `array_slice` dari depan) dihapus (`@unlink`). Jaga supaya tidak menggunung penuh disk kalau internet mati berkepanjangan.

### 8.3 Kredensial cloud
Kredensial penyimpanan awan (Oracle Object Storage + Cloudinary, disebut di komentar) **TIDAK PERNAH ada di PC sekolah** — hanya ada di sisi Hub API/VPS. PC sekolah hanya kirim file terenkripsi lewat endpoint `/api/v1/backup/upload`.

### 8.4 `backup:restore` (CLI manual, TIDAK PERNAH otomatis)
Alasan tegas di komentar: *"Memulihkan itu operasi yang MENIMPA data yang sedang hidup - kalau dijalankan otomatis, satu deteksi keliru ('sepertinya data hilang') bisa menimpa database yang sebenarnya sehat dengan data beberapa hari lalu. Yang boleh otomatis: membuat backup, mengirim, memeriksa, dan memperingatkan. Menekan tombol pulihkan tetap harus keputusan manusia."*
- Usage: `php spark backup:restore <berkas.sql.enc> [--terapkan]`.
- Kalau `backup.passphrase` di `.env` kurang dari 16 karakter, CLI prompt interaktif meminta sandi.
- Validasi magic marker `"ARSIPV1"` di 7 byte pertama file.
- Buka via `DatabaseBackup::bukaBerkasEnkripsi()`, lalu validasi tambahan: hasil dekripsi harus mengandung string `"SQL"`, `"CREATE"`, atau `"DROP"` di 2000 byte pertama (heuristik "ini benar SQL, bukan sampah hasil dekripsi gagal/sandi salah").
- Simpan hasil buka ke file `.sql` (nama = nama asli minus `.enc`).
- Tanpa `--terapkan`: berhenti di sini, instruksikan user jalankan `--terapkan` atau impor manual + `php spark migrate`.
- Dengan `--terapkan`: tampilkan peringatan merah "seluruh isi database akan DITIMPA", minta ketik **"TIMPA"** (case-insensitive di CLI ini, beda dari web yang strict uppercase-compare tapi sama-sama insensitive terhadap kapitalisasi user), `terapkanSql()`, lalu instruksikan **WAJIB** jalankan `php spark migrate` setelahnya (backup berisi struktur SAAT DIBUAT, bisa lebih lama dari versi sistem sekarang).

### 8.5 `App\Libraries\DatabaseBackup` (shared logic — SATU-SATUNYA tempat yang boleh eksekusi mysqldump/mysql shell)
Dipakai bersama oleh `BackupCloud`, `BackupRestore`, DAN `User.php` (tombol web) — ditulis sekali supaya CLI dan web selalu identik perilakunya (dulu logika disalin manual per Command, gampang basi kalau salah satu diperbaiki tapi lainnya lupa).
- `dump()`: cari `mysqldump.exe` di `C:\xampp\mysql\bin\` dulu, fallback ke PATH `mysqldump`. Opsi: `--single-transaction --routines --triggers`. Output sementara ditulis ke `WRITEPATH.'backup-antrian/.dump_tmp_{random}.sql'` lalu dibaca dan dihapus. Gagal kalau exit code != 0 atau file < 100 byte.
- `terapkanSql()`: mirip, pakai `mysql.exe`, redirect stdin dari file sementara.
- `bukaBerkasEnkripsi()`: parse `ARSIPV1` header, extract salt (byte 7-23), IV (byte 23-39), ciphertext (byte 39+), turunkan kunci PBKDF2 sama seperti enkripsi, `openssl_decrypt` AES-256-CBC.
- `catatWaktu($key)` / `waktuTerakhir($key)`: baca/tulis tabel `system_settings` (`setting_key`, `setting_value`, `updated_at`) via `REPLACE INTO` (`$db->table(...)->replace(...)`). Key yang dipakai: `last_backup_manual_at`, `last_backup_online_at`.
- `jamBackupOnline()` / `simpanJamBackupOnline()`: key `backup_cloud_hour` di `system_settings`, default `12`, validasi range 0-23 (nilai rusak/kosong jatuh ke default supaya backup tidak pernah berhenti gara-gara satu baris pengaturan aneh).

## 9. Library lain yang belum tercakup modul lain

- **`App\Libraries\BoyerMoore`**: implementasi algoritma pencarian string Boyer-Moore murni (Occurrence Heuristic + Match Heuristic), method `search($data, $keyword, $fields=['nama'])` untuk filter array asosiatif case-insensitive. **Dipakai oleh `Controllers/Siswa.php` dan `Controllers/Guru.php`** (jadi ini scope modul Siswa/Guru, bukan infra murni — kemungkinan sudah dicakup agent lain; disebutkan di sini hanya untuk kelengkapan cross-check). Tidak relevan untuk auth/dashboard/sync/backup.
- `app/Libraries/PsbProcessor.php` — disebutkan eksplisit di brief sudah dicakup agent lain, hanya dirujuk di alur `pullKeputusanPsb` di atas (7.3).
- `app/Filters/PendidikanFilter.php`: blokir server-side akses ke rute modul pendidikan kalau `!is_pendidikan_module_active()`, redirect ke `/dashboard` dengan pesan error "Modul ini tidak tersedia untuk tipe instalasi saat ini (Perusahaan)." — lapisan pengaman kedua setelah sidebar menyembunyikan menu.
- `app/Filters/LoginThrottleFilter.php`: sudah dibahas di 1.4.

## 10. KONFIGURASI — DAFTAR LENGKAP VARIABEL `.env`

Dari `.env` aktual di repo (nilai contoh dev) + referensi di kode:

| Variabel | Fungsi | Dipakai di |
|---|---|---|
| `CI_ENVIRONMENT` | `development`/`production` — mode framework CI4 (error display, debug toolbar, dll) | Bootstrap CI4 standar |
| `app.baseURL` | URL dasar aplikasi (contoh dev: `http://localhost/webarsipdata-master/public/`) | `Config\App` |
| `database.default.hostname` | Host MySQL (`localhost`) | `Config\Database` |
| `database.default.database` | Nama DB (`webarsipdata_db`) | dipakai juga oleh `.bat` scripts (hardcode `webarsipdata_db` di beberapa tempat — INCONSISTENSI: kalau nama DB diubah di `.env`, `.bat` files TIDAK ikut berubah otomatis, perlu diedit manual juga) |
| `database.default.username` | User MySQL (`root`) | `Config\Database`, dipakai literal di `.bat` (`mysqldump -u root`) — sama, tidak baca `.env` |
| `database.default.password` | Password MySQL (kosong di dev) | `Config\Database` |
| `database.default.DBDriver` | `MySQLi` | `Config\Database` |
| `database.default.port` | `3306` | `Config\Database` |
| `encryption.key` | Kunci enkripsi CI4 native (`hex2bin:...`) — **BEDA** dari `backup.passphrase` (enkripsi backup AES manual sendiri, bukan pakai CI4 Encryption service) | `Config\Encryption` |
| `app.timezone` | `Asia/Jakarta` — dipakai untuk tampilan tanggal (bukan untuk hitungan "jam backup" yang eksplisit disebut WIB terpisah) | `Config\App` |
| `install_type` | `pendidikan` / `perusahaan` / `pengembang` — default `pendidikan` kalau tidak di-set | `Common.php::install_type()` |
| `hub_api.url` | Base URL Hub API (tanpa trailing slash, akan di-rtrim) | `SyncPush`, `BackupCloud` |
| `hub_api.token` | Bearer token identitas unit ini | `SyncPush`, `BackupCloud` |
| `backup.passphrase` | Kalimat sandi enkripsi backup awan, **minimal 16 karakter**, HARUS SAMA di semua PC yang perlu saling membuka backup, tidak ada di `.env` dev sample (harus diisi manual di instalasi produksi) — **TIDAK ADA fallback otomatis**, sengaja | `BackupCloud`, `BackupRestore` |
| `unit` *(bukan `hub_api.unit`)* | Ditulis oleh `KONFIGURASI_HUB_API.bat` via `konfigurasi_hub_api.ps1`, TAPI **tidak dibaca oleh kode PHP manapun** — kemungkinan vestigial/dokumentasi manual saja | Tidak dipakai runtime |

Tidak ada `.env.example` di repo (dicek — hanya `.env` aktual ada). Untuk clone C#/SQLite, variabel yang WAJIB direplikasi sebagai pengaturan aplikasi: `install_type`, `hub_api.url`, `hub_api.token`, `backup.passphrase`, plus baseURL/timezone/DB path setara (SQLite tidak butuh host/user/password networked, tapi environment/mode masih relevan sebagai konsep debug vs production).

## 11. Route terkait (`app/Config/Routes.php`)

```
GET  /                      → Home::index (redirect ke dashboard atau login)
GET  /dashboard              → Dashboard::index          [filter: login]
POST /dev-preview/set        → DevPreview::set            [filter: login]
GET  /user                   → User::index                [filter: login (+ role:admin dari Filters.php)]
POST /user/update            → User::update
POST /user/password          → User::updatePassword
POST /user/backup-manual     → User::backupManual
POST /user/jadwal-backup     → User::simpanJadwalBackup
POST /user/restore           → User::restore
```
`/login`, `/logout`, `/register`, `/forgot`, `/reset-password` didaftarkan otomatis oleh vendor `myth/auth` (lihat `vendor/myth/auth/src/Config/Routes.php`), TIDAK muncul di `app/Config/Routes.php` aplikasi ini.

## 12. Skrip Windows terkait siklus hidup, penjadwalan, dan auto-update (konteks operasional yang harus direplikasi sebagai layanan/scheduler di aplikasi desktop .NET)

- **`START_WEBARSIPDATA.bat`**: start MySQL+Apache kalau belum jalan, auto-backup harian polos ke `backup/webarsipdata_{tanggal}.sql` (rotasi 7 file terbaru berdasar HITUNGAN file bukan umur hari — supaya PC yang jarang nyala tetap simpan 7 backup asli), lalu tunggu window dibuka manual.
  - **PENTING — mekanisme `update.lock`**: sebelum start, cek `update.lock` di root project. Kalau ada, tunggu loop `timeout 2 detik` sampai file hilang, MAKSIMAL **300 iterasi × 2 detik = 10 menit**. **BUG NYATA ditemukan 2026-09-02 (dilaporkan user — sistem macet total)**: sebelumnya loop TIDAK punya batas waktu — kalau `UPDATE.bat` terhenti di tengah (mati listrik, CMD ditutup paksa, antivirus) sebelum sempat hapus `update.lock`, file tertinggal SELAMANYA dan sistem tidak akan pernah menyala tanpa campur tangan manual. Sekarang: setelah 10 menit dianggap **stale/basi** (ditinggal proses yang mati), dihapus paksa, dicatat ke `update.log`, dan sistem tetap dijalankan (dianggap lebih aman daripada macet selamanya, risiko terburuk hanya mengakses kode yang sedang di-update yang toh sudah 10 menit tidak bergerak = kemungkinan besar memang sudah mati). **Untuk clone C#: WAJIB replikasi pola "lock file dengan timeout stale-detection", jangan biarkan lock tanpa batas waktu.**
- **`UPDATE.bat`**: cek update via `git fetch origin main` + compare SHA lokal vs remote. Kalau beda: pasang `update.lock`, backup pra-update (`pre_update_{tanggal_jam}.sql`, rotasi 7 terbaru), `git reset --hard origin/main`, `composer install`, `php spark migrate --all`. Kalau composer/migrate GAGAL → **ROLLBACK penuh**: kode dikembalikan (`git reset --hard {SHA lama}`) DAN **database juga dipulihkan dari backup pra-update** (diperbaiki 2026-08-27 — sebelumnya rollback cuma menyentuh kode, padahal migrasi gagal di tengah bisa meninggalkan struktur setengah jadi = campuran lama & baru, lebih sulit dipulihkan daripada gagal update biasa). Hapus `update.lock` di kedua jalur (sukses maupun rollback). Lanjut jalankan `START_WEBARSIPDATA.bat`.
- **`PASANG_AUTOSTART.bat`**: daftarkan 3 Windows Scheduled Task (butuh Administrator): "WebArsipData AutoStart" (trigger `onlogon`, jalankan `UPDATE.bat` via VBS hidden — dipilih trigger "saat login" bukan "saat boot" supaya tidak terganggu Fast Startup Windows), "WebArsipData SyncPush" (`sc minute /mo 1` — **tiap 1 menit**, Task Scheduler otomatis skip kalau eksekusi sebelumnya masih berjalan sehingga tidak overlap), "WebArsipData BackupCloud" (`sc minute /mo 30` — cek tiap 30 menit, jam actual diambil dari pengaturan Admin di halaman Setting bukan dipaku di scheduler).
- **`SYNC_PUSH.bat`** / **`BACKUP_CLOUD.bat`**: thin wrapper CLI (`php spark sync:push >> log`, `php spark backup:cloud >> log`), log ke `writable/logs/sync-push.log` dan `writable/logs/backup-cloud.log`.
- **`KONFIGURASI_HUB_API.bat`** + `konfigurasi_hub_api.ps1`: wizard interaktif untuk isi `hub_api.url`, `hub_api.token`, `unit` ke `.env`, dengan verifikasi tertulis (`VERIFIED_OK`), opsi langsung tes `php spark sync:push`.
- **`PERBAIKI_AKUN_ADMIN.bat`**: perbaikan manual grup admin (dibahas di 1.4) — mengandung catatan bug penting: `auth_groups.name` dan `auth_groups_users` **TIDAK punya unique index** sehingga `INSERT IGNORE` tidak mencegah duplikat (kejadian nyata: 1 database berakhir dengan 4 grup "admin" identik karena script dijalankan berulang). Solusi di script: pakai `WHERE NOT EXISTS` bukan `INSERT IGNORE`, plus langkah merapikan duplikat grup admin yang sudah terlanjur ada (pindahkan keanggotaan ke grup ber-id terkecil, hapus sisanya). **Untuk clone C#/SQLite: pastikan constraint UNIQUE benar-benar dipasang di skema baru pada kolom setara (nama grup/role) supaya kelas bug ini tidak mungkin terjadi sejak awal.**

## 13. RINGKASAN "Perilaku halus / edge case yang mudah terlewat" (kompilasi semua penanda eksplisit di kode)

1. **BUG NYATA (2026-09-02)** — `update.lock` tanpa batas waktu tunggu menyebabkan sistem macet total kalau proses update terhenti di tengah jalan; diperbaiki dengan stale-lock timeout 10 menit + auto-delete + log ke `update.log` (§12).
2. **BUG NYATA (2026-08-28)** — rotasi backup manual (`user/backup-manual`) tidak ada sama sekali sebelumnya (hanya backup harian punya rotasi), menyebabkan file menumpuk selamanya tiap tombol diklik; diperbaiki dengan rotasi 7 file terbaru (§1.6).
3. **BUG NYATA (kejadian nyata, tanpa tanggal eksplisit di komentar tapi disebut "ditemukan saat audit")** — duplikasi grup `admin` karena `auth_groups`/`auth_groups_users` tidak punya unique index dan script lama pakai `INSERT IGNORE`; solusi `WHERE NOT EXISTS` (§12).
4. **BUG (2026-08-26)** — regex nama lama menolak SEMUA 45 nama guru resmi sekolah (tidak menerima titik/koma untuk gelar akademik); diperbaiki via `REGEX_NAMA` di `BaseController` (§4).
5. **BUG (2026-09-07)** — link sidebar "Arsip Siswa & Guru" hanya mengarah ke arsip siswa, arsip guru/pegawai tidak pernah terjangkau dari sidebar sekolah; dipecah jadi 2 baris menu terpisah (§ sidebar, bagian 12 di code walk).
6. **SENGAJA** minta ketik "TIMPA" persis (bukan cukup centang) sebelum restore — operasi menghapus SELURUH database aktif dan tidak bisa dibatalkan setelah berjalan (§1.6, §8.4).
7. **SENGAJA** `backup:restore` selalu manual, tidak pernah otomatis — auto-restore dianggap terlalu berbahaya (satu deteksi keliru bisa menimpa data sehat) (§8.4).
8. **SENGAJA** menolak jalan (bukan generate kunci acak sendiri) kalau `backup.passphrase` < 16 karakter — kunci auto-generate yang hanya ada di 1 PC berisiko membuat backup permanen tidak bisa dibuka kalau PC itu mati (§8.2).
9. **SENGAJA** file gagal terkirim ke Hub API (baik sync data maupun backup) dibiarkan mengendap di antrian/writable, dicoba lagi siklus berikutnya — desain tahan-internet-mati (§7.6, §8.2).
10. **SENGAJA** `pullKeputusanPsb` dipanggil SETELAH `pushCalonSiswa` dalam urutan `run()` (§7.3).
11. **SENGAJA** LoginThrottle pakai backoff bertahap, BUKAN lockout permanen — mencegah orang lain mengunci akun korban hanya dengan salah password berulang kali (§1.4).
12. **PENTING**: fingerprint sync HARUS menyertakan versi protokol (bukan cuma hash isi data) — kalau BENTUK payload berubah tanpa menaikkan versi, PC yang datanya kebetulan identik dengan kiriman lama tidak akan pernah mengirim ulang (data yang sudah dihapus di lokal tetap "menghantui" konsumen selamanya) (§7.4).
13. **PENTING**: fingerprint hanya disimpan setelah SEMUA baris diterima sukses (`errorCount===0`) — kalau ada baris ditolak sebagian, TIDAK disimpan supaya baris gagal tidak "terkunci" tak pernah dikirim ulang (§7.4).
14. **PENTING**: `full=true` (snapshot penuh) WAJIB untuk entitas yang barisnya bisa dihapus admin (kalender, jadwal, jam, mapel, wali-kelas, kepala-sekolah) — tanpa flag ini, Hub API tidak berani menganggap baris yang hilang dari kiriman sebagai "terhapus". Tabel kosong pada entitas ber-snapshot tetap dikirim karena "kosong" adalah kabar itu sendiri (§7.5).
15. **PENTING** (implementasi CI4 CURLRequest): multipart upload harus pakai `CURLFile` dalam peta field=>nilai, BUKAN format array-of-parts Guzzle, atau akan ditolak HTTP 400 (§8.2).
16. **CATATAN ARSITEKTUR PENTING**: `*_source_id` di semua payload sync SELALU ID asli lokal (bukan ID hasil sync Hub API); konsumen resolve relasi lewat pasangan (unit_id via token, source_id lokal) — bukan foreign key global lintas sistem (§7.7).
17. **Kemungkinan dead code**: `chartLabels`/`chartData` (data per-kelas untuk chart) dihitung di `Dashboard::index()` tapi tidak dirender di `dashboard/index.php` manapun — hanya `perTingkatLabel`/`perTingkatData` (agregat per tingkat) yang dipakai (§2.2). Perlu diputuskan apakah ini fitur yang hilang atau memang sengaja dibuang saat re-write.
18. **Kemungkinan vestigial**: variabel `unit` ditulis ke `.env` oleh wizard konfigurasi Hub API tapi tidak pernah dibaca kode PHP manapun — instalasi teridentifikasi murni lewat `hub_api.token` (§7.1, §10).
19. **Inkonsistensi potensial**: `User::updatePassword()` (ganti password sendiri di halaman Setting) hanya validasi `min_length[8]` + `matches`, TIDAK menjalankan `passwordValidators` Myth Auth (`CompositionValidator`, `NothingPersonalValidator`, `DictionaryValidator`) yang berlaku di alur reset password resmi — password lemah bisa lolos lewat satu jalur tapi tidak jalur lain (§6).
20. **Inkonsistensi potensial**: nama database, user MySQL di `.bat` scripts (`webarsipdata_db`, `root`) hardcoded, tidak baca dari `.env` — kalau `.env` diubah, `.bat` harus diedit manual terpisah (§10).
21. Fitur "Lupa Password via email" (`activeResetter`) masih aktif secara konfigurasi (tidak di-override ke null seperti `requireActivation`), tapi kemungkinan besar tidak fungsional di instalasi sekolah nyata kalau SMTP tidak dikonfigurasi — perlu klarifikasi apakah ini benar dipakai atau efektif mati (§6).
