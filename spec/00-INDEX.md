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
| Halaman/Controller: **Ekskul** (CRUD, arsip, kelola peserta) | Selesai & teruji end-to-end. **Modul "Guru, Kelas, Struktur" (spec 02) kini 100% SELESAI dibangun.** |
| Halaman/Controller: **Kalender Akademik** (CRUD, kalender visual, import Excel dgn parser tanggal Indonesia fleksibel, validasi anti-salah-tahun) | Selesai & teruji end-to-end, TERMASUK uji regresi bug historis "strtotime salah tahun" (§7) — "7 Juni 2027" terverifikasi ke-parse sebagai 2027, bukan mundur ke 2026. |
| Halaman/Controller: **Akademik** (Kenaikan Kelas, Kelulusan, Arsip Historis, Rekap Lulusan) | Selesai & teruji end-to-end. |
| Halaman/Controller: **Kurikulum lengkap** (Mata Pelajaran CRUD+import, Struktur Kurikulum/Alokasi JP matrix+salin+import, Jam Belajar dgn 2 aturan bentrok+salin+import) | Selesai & teruji end-to-end. |
| Halaman/Controller: **Jadwal Pelajaran** (grid per-kelas kode "35K", parser `bacaKode()`, deteksi bentrok guru lintas kelas, salin TA sama/beda, Piket replace-all, Cetak semua kelas, Per Guru+beban, import Excel dgn pencocokan kolom dinamis+prioritas kolom-tak-dikenal) | Selesai & teruji end-to-end. **Spec 03 (Kurikulum, Jadwal Pelajaran, Kalender Akademik, Akademik) kini 100% SELESAI dibangun.** |
| Halaman/Controller: **Auth (Login/Setup Awal/Logout, cookie auth, RBAC role admin, rate-limit login bertahap) + Dashboard (Pendidikan+Perusahaan, panel Langkah Persiapan Awal) + Setting (profil+password+backup manual+jadwal backup+restore) + DevPreview** | Selesai & teruji end-to-end. **LINGKUP DIPERSEMPIT SENGAJA (didokumentasikan)**: fitur "Lupa Password via email" TIDAK diporting (PHP asli sendiri kemungkinan besar tidak fungsional tanpa SMTP - lihat 04-infra-auth-sync.md §6/§21); `allowRegistration=false` PHP diganti "Setup Awal" 1x saat tabel Users kosong (CLI `spark auth:create_user` tidak masuk akal utk distribusi desktop end-user - lihat catatan modul di bawah); cakupan `role:admin` PHP yang SPOTTY (cuma sebagian controller di `Filters.php`) diganti proteksi KONSISTEN: SEMUA controller wajib login (global filter), controller yang PHP asli tandai `role:admin` (Siswa/CalonSiswa/Guru/Kelas/TahunAjaran/Akademik/User) tetap `[Authorize(Roles="admin")]` - superset lebih aman, bukan replikasi celah. Sinkronisasi Hub API (SyncPush) & BackupCloud (upload terenkripsi ke awan) BELUM diporting - lihat baris terpisah di bawah. |
| Halaman/Controller: **Sinkronisasi Hub API** (`HubApiSyncService` - port `SyncPush.php`, 11 entitas push + pull keputusan PSB, fingerprint hemat-jaringan, full-snapshot vs non-full) **+ Backup Awan terenkripsi** (`BackupCloudHostedService` - port `BackupCloud.php`, AES-256-CBC+PBKDF2, antrian+rotasi+upload) | Selesai & **diuji end-to-end terhadap instance hub-api LOKAL SUNGGUHAN** (`C:\xampp\htdocs\hub-api`, MySQL asli, bukan mock) - lihat catatan modul di bawah untuk rincian & 1 bug nyata ditemukan+diperbaiki. |
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

## Catatan modul Ekskul

- File: `Controllers/EkskulController.cs`, `Models/Ekskul/EkskulViewModels.cs`,
  `Views/Ekskul/*.cshtml`. Modul PALING SEDERHANA/independen di spec 02 - tidak
  bergantung Guru/Kelas sama sekali (Pembina teks bebas, bukan FK).
- **Edge-case yang WAJIB dipertahankan** (bukan bug untuk diperbaiki): (1) kartu
  "Total Peserta" di Index menghitung SEMUA baris ekskul_siswa tanpa filter status
  siswa (beda dari halaman Detail yang filter status=aktif saja - §12 poin 20);
  (2) `AddSiswa` melaporkan JUMLAH YANG DICENTANG di pesan sukses, bukan jumlah yang
  benar-benar diinsert (cek duplikat manual sebelum insert, form disubmit dobel tidak
  berhenti di tengah loop - §12 poin 21).
- **Diuji end-to-end via HTTP nyata**: create ekskul, tambah peserta (siswa masuk
  daftar peserta), keluarkan peserta (siswa kembali muncul di daftar "belum ikut",
  bukan hilang permanen), arsip (riwayat peserta tetap utuh, tidak terhapus).
- Tidak ada bug baru ditemukan di modul ini.

**Modul "Guru, Kelas, Tahun Ajaran, Penugasan Mengajar, Kepala Sekolah, Ekskul"
(spec 02) kini 100% SELESAI dibangun dan teruji end-to-end.**

## Catatan modul Kalender Akademik

- File: `Controllers/KalenderAkademikController.cs`,
  `Models/KalenderAkademik/KalenderAkademikViewModels.cs`,
  `Views/KalenderAkademik/*.cshtml`. Memakai `IndonesianDateService` yang SUDAH ADA
  dari modul Siswa (`ParseIndonesianDate`/`ParseIndonesianDateRange`) - TIDAK menulis
  ulang parser tanggal, cukup panggil service yang sama.
- **`RentangWajarAsync()`** (private di controller, port `rentangWajar()` PHP) adalah
  INTI fitur anti-salah-tahun - divalidasi di SEMUA jalur tulis (Store, Update, tiap
  periode hasil ProcessImport). Nama TA harus format persis "YYYY/YYYY" atau validasi
  DILEWATI (null) - APA ADANYA dari PHP asli.
- **Keputusan desain eksplisit didokumentasikan**: `Views/KalenderAkademik/Import.cshtml`
  SENGAJA TIDAK meniru teks instruksi PHP asli yang basi/tidak sesuai perilaku
  sesungguhnya (lihat 03-akademik-jadwal.md §6 "KETIDAKSESUAIAN DOKUMENTASI") -
  di sini ditulis instruksi yang benar-benar mencerminkan parser fleksibel yang
  diimplementasikan, sesuai rekomendasi eksplisit spec ("ikuti PERILAKU SESUNGGUHNYA,
  bukan teks halaman import lama").
- Kalender visual (grid bulan, klik tanggal→detail, badge libur/kegiatan, auto-pindah
  ke bulan agenda terdekat) diimplementasikan fungsional lengkap via JS vanilla dari
  data JSON yang di-embed di halaman (bukan AJAX per bulan, sama seperti PHP asli).
- **Diuji end-to-end via HTTP nyata, TERMASUK uji regresi bug historis**: tambah
  agenda dalam rentang wajar (diterima), tambah agenda di luar rentang wajar/salah
  tahun (DITOLAK dgn pesan verbatim), import Excel dgn 5 format tanggal Indonesia
  berbeda (rentang 1 bulan, "&" dua tanggal terpisah, tanggal tunggal, DAN yang
  PALING PENTING: **"7 Juni 2027" diverifikasi ke-parse sebagai tahun 2027** - bug
  historis PHP asli `strtotime()` yang membuatnya salah baca jadi 2026 TIDAK terulang
  di implementasi C# ini), serta 1 baris ambigu ("Okt/Nov 2026") yang benar-benar
  DITOLAK bukan ditebak.
- Tidak ada bug baru ditemukan di modul ini.

## Catatan modul Akademik (Kenaikan Kelas, Kelulusan, Arsip, Rekap Lulusan)

- File: `Controllers/AkademikController.cs`, `Models/Akademik/AkademikViewModels.cs`,
  `Views/Akademik/*.cshtml`. BEDA TOTAL cakupan dari Kurikulum/Jadwal/Kalender - modul
  ini murni proses akhir tahun ajaran (kenaikan kelas & kelulusan) + arsip historisnya.
- **Perilaku SENGAJA "vestigial" direplikasi apa adanya**: `ArsipBulkDelete`/
  `RekapBulkDelete` TIDAK PERNAH benar-benar menghapus apapun - toolbar bulk-delete
  di UI cuma hiasan, endpoint SELALU balas pesan penolakan verbatim ("Riwayat akademik
  tidak dapat dihapus karena dipakai sebagai arsip permanen." / "Rekap lulusan tidak
  dapat dihapus karena dipakai sebagai arsip permanen."). JANGAN "perbaiki" ini jadi
  benar-benar menghapus.
- **Invarian penting direplikasi persis**: proses Kelulusan MENGECEK dulu apakah
  riwayat siswa+TA sudah ada (mis. siswa itu sebelumnya "naik" di TA yang sama, proses
  dalam sesi kerja yang sama) - kalau ADA, di-UPDATE jadi status "lulus" (bukan insert
  baris baru, cegah duplikat unique(SiswaId,TahunAjaranId)); kalau BELUM, baru insert.
- **Penyesuaian skema (didokumentasikan, bukan penyimpangan diam-diam)**: PHP asli
  menyimpan literal `0` untuk `kelas_id` riwayat kalau siswa tidak punya kelas saat
  lulus (kolom NOT NULL tanpa FK). Skema C# di sini men-declare `RiwayatAkademik.KelasId`
  sbg nullable sejak awal (lihat komentar entity) - jadi dipakai `null` di kondisi yang
  sama, representasi lebih benar tanpa mengubah perilaku yang terlihat user.
- **Diuji end-to-end via HTTP nyata**: proses naik kelas (siswa pindah kelas, status
  tetap aktif, riwayat "naik" tercatat), proses kelulusan (status jadi lulus, kelas_id
  jadi null, riwayat "lulus" tercatat), Arsip menampilkan badge yang benar ("Naik ke
  {kelas tujuan}" / "Lulus"), Rekap Lulusan menampilkan lulusan, dan bulk-delete
  DITOLAK dgn pesan verbatim persis.
- Tidak ada bug baru ditemukan di modul ini.

## Catatan modul Kurikulum lengkap (Mata Pelajaran, Struktur Kurikulum/Alokasi JP, Jam Belajar)

- File: `Controllers/KurikulumController.cs` (SATU controller untuk 4 sub-fitur: Master
  Tingkat yang sudah ada dari sesi sebelumnya + 3 tab baru sesi ini), `Models/Kurikulum/
  KurikulumViewModels.cs`, `Views/Kurikulum/Index.cshtml` (3-tab: Mata Pelajaran/Struktur
  Kurikulum/Jam Belajar), `Views/Kurikulum/Import.cshtml` (SATU view dipakai bergantian
  utk ke-3 jenis import lewat `ViewBag.Jenis`, pola sama `Views/Siswa/Import.cshtml`).
- **Alokasi JP ("Struktur Kurikulum")**: matriks Tingkat×MataPelajaran per Tahun Ajaran.
  Baris TIDAK ADA = mapel tidak diajarkan di tingkat itu; input 0/kosong saat simpan =
  HAPUS baris (bukan simpan nilai 0) - beda makna "tidak diajarkan" vs "diajarkan 0 jam"
  dipertahankan persis spec §5.3. `SalinAlokasi`/`SalinJam` menyalin dari TA lain TANPA
  menimpa baris yang sudah ada di TA tujuan.
- **Jam Belajar**: 2 aturan bentrok WAJIB diterapkan identik di jalur manual (`StoreJam`)
  MAUPUN import Excel (`ProcessImportJam`) - (1) nomor jam ke- bentrok di hari yang sama,
  (2) rentang waktu tumpang tindih (formula `mulai<selesai_baru AND selesai>mulai_baru`).
  **`UpdateJamBatch()` SENGAJA TIDAK mengecek ulang kedua aturan itu** (hanya validasi
  `mulai<selesai`) - inkonsistensi NYATA dari PHP asli, diverifikasi via test regresi
  eksplisit (batch-update dipaksa membuat 2 baris tumpang tindih waktu → BERHASIL masuk,
  bukan ditolak) supaya celah ini tidak "diperbaiki" tanpa sengaja di masa depan.
- **2 bug nyata ditemukan & diperbaiki selama uji coba** (pola baru, belum pernah muncul
  di modul-modul sebelumnya):
  1. `UpdateMapelBatch` (banyak baris disimpan via SATU `SaveChangesAsync()` di akhir)
     mengecek keunikan nama/kode PER BARIS ke DATABASE saja - kalau 2 baris DALAM BATCH
     YANG SAMA diubah jadi nama identik, keduanya lolos cek individual (karena baris
     lawannya belum ter-commit ke DB saat dicek) lalu SqliteException UNIQUE constraint
     mentah-mentah muncul saat `SaveChangesAsync()`. **Fix**: hitung dulu SELURUH state
     akhir (baris diedit + baris tidak diedit) jadi satu `Dictionary` di memori, deteksi
     tabrakan nama/kode LINTAS seluruh state itu (bukan query DB per baris), baru terapkan
     yang tidak tabrakan.
  2. `ProcessImportMapel`/`ProcessImportJam` punya bug SERUPA: validasi tiap baris (nama
     mapel duplikat / jam bentrok) query ke DB, TAPI `SaveChangesAsync()` cuma dipanggil
     SEKALI di akhir loop - baris ke-3 dalam file yang sama tidak "melihat" baris ke-1/ke-2
     yang baru ditambahkan (belum ter-commit), sehingga 2 baris identik dalam 1 file yang
     SAMA-SAMA seharusnya cuma 1 yang masuk malah keduanya lolos validasi lalu crash di
     `SaveChangesAsync()` (utk Jam: pernah bikin 3 baris `jam_ke=1` lolos sekaligus).
     **Fix**: panggil `SaveChangesAsync()` LANGSUNG setelah tiap baris valid ditambahkan
     (bukan sekali di akhir) - pola ini SUDAH BENAR dari awal di `ProcessImportAlokasi`
     (lewat `SimpanSelAsync` yang sudah per-sel `SaveChangesAsync`), jadi tidak perlu diubah.
     **Pelajaran umum utk modul Jadwal Pelajaran nanti**: setiap kali sebuah loop impor
     Excel melakukan "cek-lalu-`Add()`" berulang dgn kemungkinan baris saling terkait
     (bentrok satu sama lain), JANGAN batch semua `Add()` lalu SATU `SaveChangesAsync()`
     di akhir - commit per baris supaya baris berikutnya melihat state ter-update.
- **Diuji end-to-end via HTTP nyata, mencakup SEMUA invarian kritis**: Mata Pelajaran
  (tambah cepat, tolak nama duplikat, batch-update dgn tabrakan SESAMA BARIS DALAM SATU
  SUBMIT terdeteksi rapi - bukan crash, hapus dgn/tanpa proteksi FK JadwalPelajaran),
  Alokasi (simpan matriks, hapus-jika-nol, salin dari TA lain tanpa menimpa), Jam Belajar
  (tambah manual dgn kedua aturan bentrok diuji terpisah, `UpdateJamBatch` diverifikasi
  TIDAK mengecek ulang bentrok - regresi sengaja, hapus dgn info dampak ke Jadwal
  Pelajaran, salin dari TA lain), 3 alur import Excel (Mata Pelajaran/Alokasi/Jam Belajar)
  masing-masing dgn campuran baris valid+invalid DAN kasus duplikat-dalam-file-yang-sama.

## Catatan modul Jadwal Pelajaran

- File: `Controllers/JadwalPelajaranController.cs`, `Models/JadwalPelajaran/
  JadwalPelajaranViewModels.cs`, `Views/JadwalPelajaran/*.cshtml` (Index=grid utama,
  PerGuru, Cetak=`Layout=null` HTML cetak murni, Import). Modul TERAKHIR dan PALING
  KOMPLEKS di spec 03 - sengaja ditunda paling akhir sesuai rencana.
- **`bacaKode()` parser** (private): regex `^(\d*)([A-Z]+)$` - nomor guru opsional di
  depan, kode mapel wajib huruf di belakang. Kode tanpa nomor ("F") = mapel TANPA guru
  tetap (BTAQ Ummi berkelompok - FITUR, bukan bug, guru boleh tetap ditambahkan kalau
  mau via kode ber-nomor "12F"). Sel kosong pada grid manual = HAPUS slot (`simpanGrid`
  memanggil `hapusSlot()`); sel kosong saat IMPORT = SKIP diam-diam (BEDA sengaja -
  import tidak pernah menghapus, cuma menambah/memperbarui, direplikasi persis §4).
- **Cek bentrok guru disederhanakan pasca-restrukturisasi 2026-08-20**: karena
  `JamPelajaranId` sudah mengandung (tahun+hari+jam ke), cukup cocokkan
  Guru+JamPelajaranId+Semester (beda kelas) - TIDAK perlu hitung overlap waktu lagi.
  Diverifikasi: guru yang sama mengajar 2 kelas di JAM YANG SAMA (JamPelajaranId sama)
  DITOLAK dgn pesan "Guru tersebut sudah mengajar {mapel} di kelas {kelas} pada jam
  yang sama.", sedangkan guru yang sama di JAM BEDA pada kelas lain DIIZINKAN (bukan
  false-positive).
- **Salin lintas TA - 2 jalur berbeda** (`Salin()`): TA sama+semester beda →
  `JamPelajaranId` dipakai LANGSUNG (identik lintas semester dalam 1 TA); TA BEDA →
  wajib dipetakan via (Hari,JamKe) karena `JamPelajaranId` berbeda per TA - tanpa
  pasangan atau kombinasi (kelas,jam) sudah terisi → dilewati diam-diam (bukan error).
  Kedua jalur diuji terpisah termasuk re-run (jalur sama → semua "sudah terisi").
- **Import Excel - kolom tak dikenal dilaporkan PALING ATAS** (unshift), prefix "KOLOM
  DILEWATI (seluruh isinya tidak ikut masuk): ..." - dampak 1 kolom rusak = 1 kelas
  kehilangan jadwal seharian, lebih prioritas dari 1 sel salah. Diuji dgn 2 skenario
  sekaligus dalam 1 file (hari tak dikenal DAN nama kelas tak dikenal di kolom
  berbeda) + kolom lain yang valid tetap diproses normal (kombinasi bentrok guru DAN
  kode mapel tak terdaftar via jalur import, bukan cuma jalur grid manual).
- **1 bug nyata ditemukan & diperbaiki** (di luar cakupan port PHP, murni konsistensi
  internal C#): `DownloadTemplate()` awalnya memilih label "Waktu" per jam_ke dari
  `JamSemua.FirstOrDefault(j => j.JamKe==jk)` TANPA urutan hari eksplisit (bergantung
  urutan default DB) - untuk jam_ke yang punya rentang waktu BEDA antar hari (mis. jam
  ke-2 = Istirahat 08:10-08:25 di Senin tapi jam pelajaran biasa 08:10-08:50 di
  Selasa), template bisa menampilkan waktu dari hari yang salah, tidak konsisten dgn
  grid Index (yang SUDAH BENAR memprioritaskan hari sesuai `HariAktif` mulai Senin).
  Fix: `DownloadTemplate()` sekarang iterasi `HariAktif` juga sebelum fallback, sama
  seperti Index.
- **Diuji end-to-end via HTTP nyata, mencakup SEMUA invarian kritis** (skenario nyata
  2 kelas beda tingkat, 2 guru, 3 mapel termasuk 1 tanpa-guru-tetap, jam kegiatan
  Istirahat): simpan grid manual (kode ber-nomor, kode tanpa nomor, error kode tak
  dikenali/mapel tak terdaftar/guru tak ditemukan diverifikasi via §4 `bacaKode()`),
  progres kurikulum kalkulasi benar (pas/kurang/lebih, warna sesuai), guru piket
  replace-all, salin TA-sama-semester-beda DAN salin lintas-TA-beda dgn pemetaan
  (hari,jam_ke), halaman Per Guru (beban mengajar akurat dari COUNT JadwalPelajaran
  langsung), halaman Cetak (grid semua kelas + legend kode/guru), import Excel dgn
  kombinasi kolom-tak-dikenal + bentrok guru + kode tak terdaftar dalam 1 file
  sekaligus, file <4 baris ditolak dgn pesan tepat, baris kegiatan diverifikasi TIDAK
  PERNAH tersentuh import (dicoba isi kode sampah di sel kegiatan → tetap diabaikan
  total, 0 tersimpan 0 dilewati - bukti `Jenis==kegiatan` di-skip SEBELUM `bacaKode()`
  dipanggil sama sekali).

**Spec 03 (Kurikulum, Jadwal Pelajaran, Kalender Akademik, Akademik) kini 100%
SELESAI dibangun dan teruji end-to-end.**

## Catatan modul Auth, Dashboard, Setting (User), DevPreview

- File: `Controllers/AuthController.cs`, `Controllers/UserController.cs`,
  `Controllers/DevPreviewController.cs`, `HomeController.cs` (dijadikan Dashboard -
  lihat poin adaptasi rute di bawah), `Services/LoginThrottleService.cs`,
  `Services/InstallTypeService.cs`, `Services/AppOptions.cs`,
  `Services/DatabaseBackupService.cs`, `Models/Auth/*`, `Models/User/*`,
  `Models/Dashboard/*`, `Views/Auth/{Login,Setup}.cshtml` (Layout=null, halaman
  standalone sebelum login), `Views/Home/{Index,IndexPerusahaan}.cshtml`,
  `Views/User/Index.cshtml`.
- **4 ADAPTASI SENGAJA didokumentasikan** (bukan gap diam-diam - PHP asli tidak
  bisa direplikasi literal krn perbedaan arsitektur web-hosted vs desktop-lokal
  dan MySQL vs SQLite):
  1. **"Setup Awal" menggantikan `allowRegistration=false` + CLI `spark
     auth:create_user`**: PHP asli SENGAJA mematikan registrasi mandiri (siapapun
     daftar dapat akses PENUH tanpa RBAC - insiden audit keamanan nyata, lihat
     §1.2) dan HANYA membuat akun lewat CLI admin. CLI tidak masuk akal utk
     distribusi desktop end-user (sekolah tidak punya admin PHP/CLI). Diganti:
     layar "Setup Awal" HANYA muncul selagi tabel `Users` BENAR-BENAR KOSONG
     (`AuthController.Setup` re-cek kosong di server SEBELUM insert, menolak kalau
     sudah ada 1 user pun) - begitu 1 akun dibuat, jalur ini TERTUTUP SELAMANYA,
     mempertahankan SEMANGAT PHP asli (tidak ada pendaftaran bebas kapan saja)
     dengan cara yang masuk akal utk instalasi 1x oleh TU sekolah sendiri.
  2. **Proteksi rute LEBIH KONSISTEN dari PHP asli, BUKAN replikasi celah**: spec
     §1.3 menunjukkan `role:admin` di `Filters.php` PHP asli HANYA dipasang di
     sebagian rute (siswa/calon-siswa/guru/kelas/tahun-ajaran/akademik/user) -
     Kurikulum/JadwalPelajaran/KalenderAkademik/Ekskul/PenugasanMengajar/
     KepalaSekolah TIDAK ADA di daftar filter PHP (kemungkinan besar celah
     konfigurasi nyata, bukan desain sengaja - tidak ada penanda "SENGAJA" di
     spec utk gap ini, beda dgn pola penandaan bug/keputusan lain di seluruh
     spec). Diputuskan TIDAK direplikasi: filter GLOBAL "wajib login" dipasang
     di `Program.cs` utk SEMUA controller (superset - tidak pernah kurang aman
     dari PHP), DITAMBAH `[Authorize(Roles="admin")]` spesifik persis di 6
     controller yang PHP secara eksplisit tandai. Praktiknya tidak ada beda
     perilaku (cuma 1 grup "admin" pernah dipakai nyata di kedua sistem), tapi
     postur keamanan kode ini LEBIH KETAT by design.
  3. **Restore = timpa file `.db` langsung, BUKAN `terapkanSql()` baris-per-baris**:
     SQLite tidak butuh replay SQL spt MySQL. "Dump" konsisten pakai `VACUUM INTO`
     (setara `mysqldump --single-transaction`, aman walau WAL aktif). Format
     enkripsi backup (AES-256-CBC, PBKDF2-SHA256 100rb iterasi, marker "ARSIPV1")
     DIPERTAHANKAN PERSIS dari §8.2 - hanya isi payload yang beda (file `.db` utuh,
     bukan teks SQL). Ekstensi diadaptasi `.db`/`.db.enc` (bukan `.sql`/`.sql.enc`).
     **Restore WAJIB restart proses** (`IHostApplicationLifetime.StopApplication()`
     dipanggil setelah file ditimpa, migrasi otomatis jalan lagi via jalur startup
     normal yang SUDAH ADA di `Program.cs` - tidak perlu logic migrate terpisah)
     - Launcher (WPF, belum dibangun) yang nanti bertanggung jawab menjalankan
       ulang proses child, persis semangat PHP asli yang minta `php spark migrate`
       manual setelah restore (§1.6, §8.4: "menekan tombol pulihkan tetap harus
       keputusan manusia", di sini "restart aplikasi" adalah langkah manusia yang
       setara). **Lapisan tambahan yang TIDAK ADA di PHP asli**: snapshot
       `pra_restore_{timestamp}.db` otomatis dibuat SEBELUM file ditimpa - murni
       jaga-jaga, PHP tidak punya ini krn karakter risiko mysqldump/restore beda.
  4. **BackupCloud (upload terenkripsi ke Hub API) & SyncPush BELUM diporting** -
     `DatabaseBackupService` SUDAH punya seluruh primitif kripto (Enkripsi/Dekripsi
     AES-256-CBC+PBKDF2 statis, format ARSIPV1 persis §8.2) siap dipakai ulang saat
     modul sync dibangun, tapi background service periodik (`backup:cloud`
     equivalent, cek jam terjadwal, kirim antrian ke `/api/v1/backup/upload`) BELUM
     ada. `AppOptions.HubApiUrl/HubApiToken/BackupPassphrase` sudah disiapkan di
     `appsettings.json` (kosong default, WAJIB diisi manual saat instalasi produksi
     - PERSIS semangat §8.2 "tidak ada fallback otomatis").
- **RBAC**: hanya grup `admin` diimplementasikan bermakna (grup lain BISA dibuat via
  skema `AuthGroup`/`AuthGroupUser` tapi tidak ada UI Manajemen Pengguna - PERSIS PHP
  asli, §1.1 "Tidak ada tabel/menu Manajemen Pengguna berbasis web sama sekali").
- **LoginThrottleService**: backoff bertahap PERSIS §1.4 (≥3→30 detik, ≥5→60 detik,
  ≥7→300 detik), key=MD5(login value), TTL cache 3600 detik, direset begitu login
  sukses - BUKAN lockout permanen (mencegah orang lain mengunci akun KORBAN dgn
  sengaja salah password berkali-kali).
- **`User::updatePassword()` inkonsistensi PHP direplikasi APA ADANYA** (§6/§19):
  ganti password sendiri di halaman Setting HANYA menegakkan `min_length[8]+matches`
  (TIDAK menjalankan Composition/NothingPersonal/DictionaryValidator yang berlaku di
  alur reset-password resmi - yang malah TIDAK diporting sama sekali, lihat poin
  "Lupa Password" di tabel status atas). Password lemah tetap BISA lolos lewat jalur
  ganti-password-sendiri, PERSIS PHP asli.
- **Dashboard perTingkat pakai kode tingkat MENTAH** (bukan lookup `Tingkat.Nama`
  spt modul lain) - `Dashboard.php` PHP asli literal `"Kelas " . $tingkat`, BUKAN
  panggil `TingkatModel`, direplikasi persis §2.2 walau beda pola dari modul lain.
- **Diuji end-to-end via HTTP nyata, mencakup SEMUA invarian kritis**: akses rute
  terproteksi tanpa login (redirect ke `/login?ReturnUrl=...`), Setup Awal HANYA
  muncul saat Users kosong lalu TERTUTUP setelah 1 akun dibuat, login sukses/gagal,
  **rate-limit login diverifikasi NYATA** (percobaan ke-4 ditolak dgn hitung mundur
  detik, kredensial BENAR sekalipun tetap ditolak selama masa tunggu, direset
  otomatis setelah masa tunggu habis DAN setelah login sukses), Dashboard Pendidikan
  (kartu statistik, panel Langkah Persiapan Awal 0/8 di database kosong), Setting
  (update profil dgn validasi `alpha_numeric_space` username - underscore DITOLAK
  benar, ganti password dgn verifikasi password lama, login ulang pakai password
  baru berhasil), **backup manual nyata dibuat via `VACUUM INTO` (berkas .db
  315KB valid diverifikasi header SQLite)**, **restore end-to-end PENUH**: upload
  balik file backup terenkripsi dgn sandi SALAH (ditolak rapi, server tetap
  hidup), sandi BENAR (snapshot pra-restore otomatis dibuat, file ditimpa, server
  berhenti via `StopApplication()` - diverifikasi proses benar2 mati), restart
  manual server (migrasi "already up to date", TIDAK error), login dgn akun HASIL
  RESTORE berhasil membuktikan pemulihan bekerja utuh dari ujung ke ujung.
- Tidak ada bug nyata ditemukan di modul ini (di luar 1 kesalahan skenario uji milik
  sendiri - username `admin_tu` sempat dikira bug krn ditolak, ternyata validasi
  `alpha_numeric_space` PHP asli memang tidak mengizinkan underscore, BENAR sesuai
  spec, bukan cacat implementasi).

## Catatan modul Sinkronisasi Hub API & Backup Awan

- File: `Services/HubApiSyncService.cs` (port `SyncPush.php` §7 - 11 method push +
  1 pull + 1 lapor-selesai + helper fingerprint/post generik), `Services/
  HubApiSyncHostedService.cs` (loop periodik 1 menit), `Services/
  DatabaseBackupService.cs` (ditambah bagian Backup Awan: `BuatBackupHariIniJikaPerluAsync`/
  `KirimAntrianAsync`/`RotasiAntrian`, port `BackupCloud.php` §8.2),
  `Services/BackupCloudHostedService.cs` (loop periodik 30 menit).
  `AppOptions` (`HubApiUrl`/`HubApiToken`/`BackupPassphrase`/`InstallType`) dibaca
  dari `appsettings.json` section `AppSettings` - kosong default, HARUS diisi
  manual saat instalasi produksi (§8.2 "tidak ada fallback otomatis").
- **ADAPTASI ARSITEKTUR SENGAJA**: PHP asli menjadwalkan `sync:push`/`backup:cloud`
  sbg proses CLI TERPISAH lewat Windows Task Scheduler (tiap 1 menit / 30 menit).
  Di sini keduanya jadi `BackgroundService` (`PeriodicTimer`) DI DALAM proses
  Kestrel yang sama - desktop app ini sudah long-running selama WebView2 terbuka,
  jadi proses CLI terpisah tidak diperlukan. Loop TUNGGAL secara alami mencegah
  overlap (setara "Task Scheduler skip kalau eksekusi sebelumnya masih berjalan").
  `do-while` membuat siklus PERTAMA jalan LANGSUNG saat app start (bukan menunggu
  interval penuh dulu) - lebih responsif dari Task Scheduler, tidak mengubah makna.
- **Fingerprint hemat-jaringan (§7.4) direplikasi PERSIS**: `sha256("v4-full|" +
  json)`, versi protokol "v4-full" SENGAJA DISAMAKAN PERSIS dgn versi payload PHP
  asli (guru sudah termasuk `is_kepala_sekolah`+`status_keluar`) - supaya PC yang
  datanya kebetulan identik dgn kiriman PHP lama TIDAK perlu kirim ulang semua saat
  berpindah ke implementasi C# ini. File fingerprint per-path (`md5(path).hash`)
  disimpan di `sync_state/` SELEVEL dgn `backup/` (BUKAN di database) - sengaja
  supaya tidak ikut ter-backup/ter-restore.
- **`kelas_source_id` guru - ADAPTASI terdokumentasi**: skema C# di sini menyimpan
  riwayat Wali Kelas PER TAHUN AJARAN (beda dari asumsi PHP lama "1 guru maks 1
  kelas SELAMANYA" via `UNIQUE(guru_id)` global) - dipetakan ke wali kelas TAHUN
  AJARAN AKTIF SAJA sbg representasi "kelas wali SAAT INI" di payload guru. Riwayat
  LENGKAP lintas tahun tetap terkirim UTUH via `pushWaliKelas` (full snapshot
  terpisah, tidak terpotong).
- **1 bug nyata ditemukan & diperbaiki SAAT uji terhadap hub-api SUNGGUHAN** (bukan
  mock - ini justru BUKTI kenapa uji end-to-end nyata wajib, bukan cuma baca kode):
  `PullKeputusanPsbAsync` awalnya parse field JSON numerik (`id`, `calon_siswa_source_id`,
  `kelas_source_id`) via `JsonElement.GetInt32()` langsung, ASUMSI semua angka JSON
  native. Ternyata baris `PsbKeputusanModel` di hub-api (CI4+MySQLi, hasil query
  DB mentah) mengembalikan SEMUA kolom sbg STRING di JSON (`"id":"3"`, bukan `"id":3`)
  - BEDA dari respons `SyncController` (counter PHP int murni `$synced++` tetap
    JSON number asli). Exception nyata: `"requires Number, target has String"`.
  **Fix**: helper `GetIntFlexible()` menerima KEDUA bentuk (`JsonValueKind.Number`
  ATAU `JsonValueKind.String` yg di-`int.TryParse`), diterapkan jg ke parsing respons
  `PostAsync` (synced/changed/unchanged/deleted) sbg pertahanan berlapis walau jalur
  itu terbukti sudah benar. **Pelajaran umum**: JANGAN asumsikan tipe JSON dari API
  eksternal PHP/MySQLi konsisten across-endpoint - baris query mentah vs counter
  terhitung PHP bisa berbeda representasi, walau sama2 "angka" secara logis.
- **Diuji end-to-end SUNGGUHAN thd hub-api lokal** (`C:\xampp\htdocs\hub-api`, MySQL
  `hubapi_db` asli dinyalakan via `mysql_start.bat`, server `php spark serve`,
  klien API test dibuat via `php spark api:create-client ... source 999`, SEMUA
  data uji+klien test DIHAPUS BERSIH dari `hubapi_db` sebelum sesi selesai):
  - **Ke-11 entitas push diverifikasi FIELD-PER-FIELD lewat baca-balik `DataController`
    Hub API** (bukan cuma "200 OK") - Kelas (label tingkat benar dari nama Tingkat,
    bukan fallback), Guru (`kelas_source_id` resolve benar dari WaliKelas, jabatan
    ikut ter-refleksi hasil auto-sync guru_bidang→guru_kelas dari modul Penugasan
    Mengajar sebelumnya, `is_kepala_sekolah` benar), Siswa, TahunAjaran, MataPelajaran,
    JamPelajaran, WaliKelas, KepalaSekolah, **JadwalPelajaran (JOIN dgn JamPelajaran
    resolve hari/jam_mulai/jam_selesai/jam_ke dgn benar, guru_source_id null utk
    mapel tanpa guru tetap)**, KalenderAkademik, PSB (dokumen_lengkap=0 benar
    krn tanpa upload dokumen) - SEMUA cocok persis data yang diinput via web.
  - **Fingerprint diverifikasi NYATA lewat 2 siklus proses berbeda**: entitas yang
    datanya TIDAK berubah antar-restart correctly SKIP kirim ("tidak berubah sejak
    sync terakhir"), entitas yang datanya BERUBAH correctly terkirim ulang - bukan
    cuma dibaca dari kode, timing race alami dari 2 proses server terpisah
    membuktikan logic compare-fingerprint bekerja benar di kedua arah.
  - **Pull keputusan PSB - SEMUA 3 skenario diuji via hub-api SUNGGUHAN** (submit
    keputusan lewat `POST /api/v1/psb/keputusan` persis cara Mobile-app asli akan
    memanggilnya): Terima→siswa baru benar2 tercipta lokal dgn NIS sesuai kiriman,
    Tolak→status calon jadi "ditolak" dgn catatan, **kelas_source_id tidak
    valid→gagal DENGAN PESAN TEPAT** ("Kelas tujuan tidak ditemukan...") TANPA
    merusak data lokal, dan `laporKeputusanSelesai` mengembalikan status
    applied/failed yang benar ke hub-api (diverifikasi langsung query tabel
    `psb_keputusan` di `hubapi_db`).
  - **Backup awan end-to-end**: berkas terenkripsi otomatis dibuat (`VACUUM INTO`
    + AES-256-CBC) dan ter-upload ke endpoint `/api/v1/backup/upload` hub-api PADA
    SIKLUS PERTAMA startup, diverifikasi lewat `GET /api/v1/backup/status` hub-api
    (ukuran berkas & waktu cocok).

**Fase berikutnya: Launcher WPF (start/stop server, WebView2, auto-update,
restart-setelah-restore) - masih kerangka splash screen saja, belum ada logic sama
sekali. Setelah itu: CI/CD GitHub Actions, dan uji coba sungguhan sbg PC TU TK
(token Hub API produksi baru, unit_id=2) sebelum dianggap siap dipakai nyata.**

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
