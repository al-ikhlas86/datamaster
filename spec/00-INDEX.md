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
| Halaman/Controller modul lain (Guru, Kelas, Tahun Ajaran, Ekskul, Kurikulum, Jadwal, Kalender, Akademik, Auth) | Belum dimulai — Kelas/TahunAjaran jadi PRIORITAS berikut krn modul Siswa/PSB sudah bergantung padanya (saat ini di-seed manual utk testing) |
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
