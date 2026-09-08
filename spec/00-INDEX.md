# Indeks Spesifikasi & Progres Pembangunan

Dokumen ini adalah peta status - baca ini dulu sebelum mulai kerja di sesi mana pun,
supaya tahu persis sudah sampai mana dan apa langkah berikutnya.

## Status Inventarisasi (baca kode PHP, TANPA mengubah apapun)

| # | Modul | File spec | Status |
|---|---|---|---|
| 1 | Siswa & PSB (Calon Siswa) | `01-siswa-psb.md` | Selesai |
| 2 | Guru, Penugasan Mengajar, Kepala Sekolah, Kelas, Tahun Ajaran, Ekskul | `02-guru-kelas-struktur.md` | Selesai (modul Kepala Sekolah §6 perlu verifikasi ulang - lihat catatan di file) |
| 3 | Kurikulum, Jadwal Pelajaran, Kalender Akademik, Akademik | `03-akademik-jadwal.md` | Selesai |
| 4 | Auth, Manajemen Pengguna, Dashboard, Sinkronisasi Hub API, Backup | `04-infra-auth-sync.md` | Selesai |

## Status Pembangunan .NET

| Bagian | Status |
|---|---|
| Solusi + 3 proyek (Data/Web/Launcher) discaffold, build sukses | Selesai |
| Skema database (EF Core entities + DbContext + migrasi awal) | Selesai — 21 entity (Siswa, CalonSiswa, Guru, Kelas, TahunAjaran, WaliKelas, KepalaSekolah, Ekskul+EkskulSiswa, MataPelajaran, GuruMataPelajaran, KurikulumAlokasi, JamPelajaran+JamPelajaranTingkat, JadwalPelajaran, JadwalPiket, KalenderAkademik, RiwayatAkademik, Tingkat, User+AuthGroup+AuthGroupUser, SystemSetting), migrasi `InitialCreate` diterapkan ke `App_Data/datamaster.db`. Enum disimpan sbg TEXT (bukan int) supaya identik pembacaan MySQL ENUM asli. Belum: field yang butuh validasi non-schema (format TA "YYYY/YYYY", regex nama, dst) — itu tanggung jawab layer aplikasi/controller nanti, bukan DbContext. |
| Halaman/Controller: **Siswa (CRUD, arsip, import upsert 3-langkah, cetak, dokumen)** | Selesai & teruji end-to-end |
| Halaman/Controller: **PSB/Calon Siswa (CRUD, filter tingkat/status, Terima→auto-generate NIS→jadi Siswa, Tolak, hard-delete dgn proteksi)** | Selesai & teruji end-to-end — spec 01 §3.17-3.28 tuntas. Modul "Siswa & PSB" (spec 01) 100% selesai dibangun. |
| Halaman/Controller: **Tahun Ajaran** (CRUD, aktifkan, delete-selalu-gagal) | Selesai & teruji end-to-end |
| Halaman/Controller: **Kelas** (CRUD, arsip+proteksi siswa aktif, kelola siswa per kelas, cetak per-kelas/per-tingkat, format-nama-otomatis anti-double-prefix) + **Master Tingkat** (`KurikulumController`, hanya 3 method Tingkat - modul Kurikulum lengkap belum) | Selesai & teruji end-to-end. **LINGKUP BELUM LENGKAP disengaja**: fitur penetapan Wali Kelas (assignWaliKelas dari PenugasanMengajar.php) BELUM diporting krn butuh modul Guru dulu (belum ada) - kolom Wali Kelas sementara nonaktif/placeholder di UI. |
| Halaman/Controller: **Guru & Pegawai** (CRUD, arsip dgn alasan keluar, import upsert 3-langkah, cetak) | Selesai & teruji end-to-end. **LINGKUP SENGAJA DIPERSEMPIT**: install_type "perusahaan" belum didukung (selalu berperilaku "pendidikan" — semua jabatan diizinkan). |
| Halaman/Controller: **Penugasan Mengajar "Guru Pengampu"** (nomor urut+deteksi bentrok, tautan guru↔mapel dgn tingkat, AJAX tanpa-reload) + **Wali Kelas** (autocomplete+autosave AJAX di halaman Kelas, sinkronisasi otomatis jabatan guru_kelas↔guru_bidang, guard tolak-ubah-jabatan) | Selesai & teruji end-to-end — CELAH yang didokumentasikan di modul Guru/Kelas sebelumnya kini TERTUTUP (kolom Wali Kelas terisi sungguhan). Termasuk `WaliKelasService` (port `WaliKelasModel.php`) dan tambahan cepat `KurikulumController.StoreMapel` (MataPelajaran belum punya CRUD sendiri). |
| Halaman/Controller: **Kepala Sekolah** (autocomplete tanpa filter eksklusif, tetapkan/kosongkan per-tahun-ajaran, reload penuh setelah AJAX - beda sengaja dari Wali Kelas/Guru Pengampu) | Selesai & teruji end-to-end. |
| Halaman/Controller modul lain (Ekskul, Kurikulum lengkap - Alokasi JP/Jam Belajar, Jadwal Pelajaran, Kalender Akademik, Akademik/Kenaikan Kelas, Auth) | Belum dimulai — modul "Guru, Kelas, Tahun Ajaran, Penugasan Mengajar, Kepala Sekolah" (spec 02, minus Ekskul) kini 100% selesai. **Ekskul jadi kandidat termudah berikutnya** (independen, tidak bergantung modul lain yang belum ada). |
| Sinkronisasi Hub API (port dari SyncPush.php) | Belum dimulai |
| Launcher (splash, start/stop server, WebView2, auto-update) | Kerangka XAML splash sudah ada (`MainWindow.xaml`); `MainWindow.xaml.cs` (logic start server + WebView2 + auto-update) belum dimulai |
| CI GitHub Actions (build+release, pola Presensi) | Belum dimulai |
| Uji coba sbg PC TU TK (token Hub API baru, unit_id=2) | Belum dimulai |

## Catatan modul Siswa (contoh pola yang WAJIB diikuti modul-modul berikutnya)

- File: `src/DataMaster.Web/Controllers/SiswaController.cs`, `Models/Siswa/SiswaViewModels.cs`,
  `Views/Siswa/*.cshtml`, `Services/IndonesianDateService.cs` (port parser tanggal Indonesia
  dari date_helper.php), `Services/TextSearchService.cs` (pengganti fungsional BoyerMoore.php),
  `Services/DocumentStorageService.cs` (upload/hapus dokumen di luar wwwroot, port pola
  WRITEPATH/uploads/dokumen PHP).
- **Diuji end-to-end via HTTP nyata** (dotnet run + curl, bukan cuma baca kode): create,
  validasi (NIS duplikat, No HP wajib), update, arsip (delete = ubah status, bukan hapus
  fisik — sesuai §3.8), import upsert (1 baris baru + 1 baris koreksi dalam 1 file,
  preview menampilkan diff yang benar, apply menerapkan dengan benar, TTL "Depok, 01
  Januari 2019" ke-parse jadi tanggal benar). Data uji sudah dibersihkan (`App_Data/datamaster.db`
  dihapus) sebelum commit — jalankan `dotnet ef database update` lagi dari `DataMaster.Web/`
  untuk membuat database kosong yang baru.
- **2 bug nyata ditemukan & diperbaiki selama uji coba** (pelajaran utk modul lain):
  1. ClosedXML `XLWorkbook`/`IXLRow` bersifat LAZY — kalau workbook di-dispose (keluar
     `using`) sebelum semua sel benar-benar dibaca, `Cell()` throw `ObjectDisposedException`.
     WAJIB materialisasi semua sel jadi `string[]` polos SELAGI workbook masih hidup,
     baru diproses di luar blok `using`.
  2. Nama action controller vs nama file view HARUS cocok (konvensi MVC) kecuali dipanggil
     eksplisit via `View("NamaFile", model)` — `ShowPreviewImport()` awalnya `return View(x)`
     padahal file view bernama `PreviewImport.cshtml`, menghasilkan 500 "view tidak ditemukan".
- **Keputusan desain didokumentasikan (bukan gap diam-diam)**: visual belum meniru tema
  Bootstrap admin ("NextAdmin") PHP asli pixel-per-pixel (sidebar, warna, komponen SweetAlert2)
  — prioritas sesi ini adalah PARITAS FUNGSIONAL & TEKS (semua pesan/aturan/alur identik
  verbatim per spec), tema visual final adalah pekerjaan lanjutan terpisah. Konfirmasi native
  browser dipakai sementara pengganti SweetAlert2 di beberapa tempat.
- CSRF: diaktifkan GLOBAL via `AutoValidateAntiforgeryTokenAttribute` (namespace
  `Microsoft.AspNetCore.Mvc`, BUKAN `.ViewFeatures` — sempat salah tebak) di `Program.cs`,
  meniru `csrf_field()` otomatis CI4. Semua `<form asp-action="...">` otomatis dapat token
  (tidak perlu `@Html.AntiForgeryToken()` manual — akan duplikat kalau ditambahkan).
- Session diaktifkan (`AddSession`+`UseSession`) khusus utk alur preview-import 2 langkah,
  TempData memakai session provider yang sama utk flash message "message"/"error"/"warning"
  (baca sekali lalu hilang, pola flashdata CI4) — dirender global via
  `Views/Shared/_FlashMessages.cshtml` yang di-include `_Layout.cshtml`.

## Catatan modul PSB/Calon Siswa

- File: `Controllers/CalonSiswaController.cs`, `Models/CalonSiswa/CalonSiswaViewModels.cs`,
  `Views/CalonSiswa/*.cshtml`, `Services/PsbService.cs` (port `PsbProcessor.php` - dipakai
  jalur web SEKARANG, dan akan dipakai LAGI oleh service sinkronisasi Hub API nanti saat
  `pullKeputusanPsb` diimplementasikan - JANGAN duplikasi logic Terima/Tolak di 2 tempat,
  selalu panggil `PsbService` yang sama seperti PHP aslinya memanggil `PsbProcessor` yang sama).
- **Diuji end-to-end via HTTP nyata** (perlu seed manual TahunAjaran+Tingkat+Kelas dulu krn
  modul Kelas/TahunAjaran belum ada UI-nya — lihat riwayat sesi ini kalau perlu pola seed
  serupa): create calon siswa, Terima→NIS auto-generate `26270001` (format prefix tahun
  ajaran "2026/2027"→"2627" + urut 4 digit, PERSIS spec §3.24) dan redirect ke halaman
  Detail Siswa hasil promosi, Tolak→redirect ke Index (BEDA dari Terima yg redirect ke
  Siswa), hard-delete calon ditolak (berhasil) vs calon diterima (DITOLAK dgn pesan
  verbatim "Calon siswa yang sudah diterima tidak bisa dihapus (sudah jadi data siswa
  resmi).").
- Tidak ada bug baru ditemukan di modul ini (pola sama modul Siswa, dan 2 bug yang sudah
  ditemukan di sesi modul Siswa tidak terulang di sini).

## Catatan modul Tahun Ajaran & Kelas (+ Master Tingkat)

- File: `Controllers/TahunAjaranController.cs`, `Controllers/KelasController.cs`,
  `Controllers/KurikulumController.cs` (BARU, HANYA berisi 3 action Master Tingkat -
  `store`/`UpdateBatch`/`delete` di route `kurikulum/tingkat/*` - route SENGAJA
  dipertahankan di bawah "kurikulum" walau UI-nya cuma muncul di halaman Kelas, persis
  arsitektur route PHP asli. JANGAN kaget nanti kalau modul Kurikulum lengkap dibangun
  dan controllernya sudah ada duluan - tinggal ditambah action-action lain di file yang sama).
- **LINGKUP SENGAJA DIPERSEMPIT (didokumentasikan, bukan gap diam-diam)**: fitur
  penetapan Wali Kelas (form autocomplete guru multi-slot di halaman Kelas PHP asli,
  lihat 02-guru-kelas-struktur.md §7.2/§13.11) BELUM diporting - butuh data Guru yang
  belum ada modelnya di UI. `Edit.cshtml` menampilkan field Wali Kelas sbg readonly
  placeholder "(belum tersedia - modul Guru belum dibangun)". Ini HARUS diselesaikan
  saat modul Guru/PenugasanMengajar dibangun - jangan anggap Kelas "selesai total"
  sampai itu tersambung.
- **Diuji end-to-end via HTTP nyata**: buat+aktifkan Tahun Ajaran, tambah Tingkat lewat
  panel di halaman Kelas (route `kurikulum/tingkat/store` dgn `kembali_ke=kelas`), tambah
  Kelas dgn verifikasi `formatNamaKelas()` (auto-prefix "Kelas 1 Al Ikhlas" dari input
  "Al Ikhlas", DAN anti-double-prefix kalau user sudah ketik "Kelas 1 ..." sendiri),
  penolakan nama kelas duplikat per-tingkat, integrasi penuh lintas modul (siswa
  ditempatkan ke kelas via `Siswa::Store`, kartu total siswa di index Kelas ter-update,
  arsip kelas DITOLAK selama masih ada siswa aktif dgn pesan jumlah yang benar,
  keluarkan siswa lalu arsip berhasil, restore mengembalikan status aktif).
- Tidak ada bug baru ditemukan di modul ini.

## Catatan modul Guru & Pegawai

- File: `Controllers/GuruController.cs`, `Models/Guru/GuruViewModels.cs`, `Views/Guru/*.cshtml`.
  Pola SAMA PERSIS modul Siswa (index+search+pagination AJAX, import upsert 3-langkah
  via session, arsip bukan hapus fisik) - baca kode Siswa dulu kalau mau memahami polanya.
- **1 bug nyata ditemukan &amp; diperbaiki saat uji coba**: helper lokal `ToRowsAsync` menerima
  `IQueryable&lt;Guru&gt;` lalu memanggil `.ToListAsync()` (ekstensi ASYNC EF Core) - ini SAH untuk
  query yang datang dari `db.Guru...` (provider EF Core), TAPI query mode pencarian
  membangun ulang `IQueryable` dari `List&lt;Guru&gt;` in-memory via `.AsQueryable()` (provider
  LINQ-to-Objects biasa) - EF Core `.ToListAsync()` melempar
  `InvalidOperationException: The source 'IQueryable' doesn't implement 'IAsyncEnumerable'`
  begitu dipanggil di atas provider bukan-EF. **Pelajaran umum utk modul lain**: kalau
  sebuah helper dipakai gantian utk data HASIL QUERY DB dan data HASIL FILTER IN-MEMORY,
  jangan pakai satu tipe parameter `IQueryable` generik dgn `.ToListAsync()` di dalamnya -
  pisahkan jadi 2 varian (satu terima `IQueryable` + `.ToListAsync()`, satu terima
  `List&lt;T&gt;` sudah jadi), seperti pola `ToRowsFromDbAsync()`/`ToRowsAsync()` di sini.
- Guard perubahan jabatan (menolak ubah jabatan guru yang masih tercatat wali kelas) sudah
  diimplementasikan BENAR walau saat ini tidak akan pernah ter-trigger nyata (karena belum
  ada cara menetapkan wali kelas) - forward-compatible, jangan dihapus/disederhanakan.
- **Diuji end-to-end via HTTP nyata**: create, validasi No HP duplikat (scoped ke
  status_aktif=true, persis spec), arsip dgn alasan keluar (`status_keluar`), import upsert
  (1 baris baru + 1 baris koreksi gelar_terakhir saja, field lain tidak berubah).

## Catatan modul Penugasan Mengajar (Guru Pengampu + Wali Kelas)

- File: `Services/WaliKelasService.cs` (port `WaliKelasModel.php` - dipakai BERSAMA oleh
  `GuruController` (baca status wali utk guard+tampilan) dan `KelasController`/
  `PenugasanMengajarController` (baca+tulis assignment) - JANGAN duplikasi logic tetapkan/
  tetapkanSemua di tempat lain, semua tulis WAJIB lewat service ini persis PHP asli yang
  semua tulis lewat 1 model yang sama), `Controllers/PenugasanMengajarController.cs`,
  `Models/PenugasanMengajar/PenugasanMengajarViewModels.cs`,
  `Views/PenugasanMengajar/Index.cshtml`. UI Wali Kelas SENDIRI ada di
  `Views/Kelas/Index.cshtml` (combobox multi-slot autosave), BUKAN di halaman Guru
  Pengampu - persis arsitektur route PHP asli (endpoint di controller Penugasan Mengajar,
  tapi UI-nya di halaman Kelas).
- **MataPelajaran belum punya CRUD sendiri** (modul Kurikulum lengkap belum diporting) -
  ditambahkan `KurikulumController.StoreMapel` (tambah cepat) supaya Guru Pengampu bisa
  dipakai. Ini SEMENTARA - saat modul Kurikulum lengkap dibangun, method ini akan
  digabung ke situ (jangan kaget menemukan `StoreMapel` "nyempil" di controller yang
  namanya nyaris sama dgn method Tingkat).
- **Diuji end-to-end via HTTP nyata, mencakup SEMUA invarian kritis**: autocomplete
  cari-guru-kelas, assign wali kelas via AJAX (autosave tanpa reload), **sinkronisasi
  otomatis jabatan guru_bidang→guru_kelas saat ditetapkan jadi wali**, **guard PENTING:
  menolak ubah jabatan guru yang masih tercatat wali kelas** (pesan verbatim persis spec),
  kosongkan wali kelas (jabatan kembali ke guru_bidang otomatis), tambah mata pelajaran,
  tautkan guru↔mapel dgn nomor urut via AJAX, **deteksi bentrok nomor urut** (pesan
  verbatim persis spec "Nomor 35 sudah dipakai oleh..."), hapus tautan mapel.
- Tidak ada bug baru ditemukan di modul ini - kemungkinan besar krn `WaliKelasService`
  ditulis SANGAT dekat dgn spec §11 (activeTahunAjaranId/getKelasByGuru/tetapkanSemua/
  sinkronJabatanGuru dgn urutan operasi PERSIS sama).

## Catatan modul Kepala Sekolah

- File: `Services/KepalaSekolahService.cs` (port `KepalaSekolahModel.php`),
  `Controllers/KepalaSekolahController.cs`, `Views/KepalaSekolah/Index.cshtml`.
  Pola SANGAT mirip Wali Kelas TAPI dengan 3 beda SENGAJA yang JANGAN disamakan:
  (1) `CariKandidat` TANPA filter eksklusif (guru yang sudah wali kelas TETAP bisa
  jadi kandidat kepala sekolah - kepala sekolah bukan jabatan, cuma flag tambahan);
  (2) SETELAH `Tetapkan()` sukses via AJAX, JS SELALU `window.location.reload()`
  (beda dari Wali Kelas/Guru Pengampu yang eksplisit menghindari reload - lihat
  02-guru-kelas-struktur.md §6.9); (3) TIDAK ADA guard silang ke modul Guru (gap
  nyata didokumentasikan §6.10 - kepala sekolah yang dinonaktifkan lewat menu Guru
  TETAP tampil sebagai "saat ini" sampai di-Kosongkan manual - direplikasi APA ADANYA).
- **Diuji end-to-end via HTTP nyata**: state kosong ("Belum ada Kepala Sekolah
  ditetapkan."), autocomplete cari-kandidat, tetapkan (redirect ke `?tahun=` dgn
  message verbatim), kosongkan (message verbatim, balik ke state kosong).
- Tidak ada bug baru ditemukan di modul ini.

## Prinsip wajib dipegang tiap sesi lanjutan

1. **Jangan pernah asumsi** - kalau spec belum menjawab suatu detail, BACA ULANG kode PHP
   aslinya (`c:\xampp\htdocs\webarsipdata-master`) sebelum menebak.
2. **Jangan sentuh web PHP PC TU SD** - proyek ini 100% terpisah, cuma BACA kodenya sbg
   acuan.
3. Tiap modul yang selesai dibangun, uji SISI-BERSAMPINGAN dgn web PHP yang sekarang
   (input sama, harus hasil sama) sebelum ditandai selesai — DAN uji end-to-end via HTTP
   nyata (dotnet run + curl/browser), bukan cuma baca kode, seperti pola modul Siswa di atas.
4. Update tabel status di file ini tiap akhir sesi kerja.
5. Sebelum commit: hapus `App_Data/datamaster.db` (data uji) - file ini memang di-gitignore,
   tapi jangan biarkan proses `dotnet run` lama masih mengunci-nya saat commit/build berikutnya.
