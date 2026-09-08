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
| Halaman/Controller per modul | Belum dimulai — mulai dari modul Siswa & PSB (spec 01) sesuai urutan alfabetis paling sederhana dulu |
| Sinkronisasi Hub API (port dari SyncPush.php) | Belum dimulai |
| Launcher (splash, start/stop server, WebView2, auto-update) | Kerangka XAML splash sudah ada (`MainWindow.xaml`); `MainWindow.xaml.cs` (logic start server + WebView2 + auto-update) belum dimulai |
| CI GitHub Actions (build+release, pola Presensi) | Belum dimulai |
| Uji coba sbg PC TU TK (token Hub API baru, unit_id=2) | Belum dimulai |

## Prinsip wajib dipegang tiap sesi lanjutan

1. **Jangan pernah asumsi** - kalau spec belum menjawab suatu detail, BACA ULANG kode PHP
   aslinya (`c:\xampp\htdocs\webarsipdata-master`) sebelum menebak.
2. **Jangan sentuh web PHP PC TU SD** - proyek ini 100% terpisah, cuma BACA kodenya sbg
   acuan.
3. Tiap modul yang selesai dibangun, uji SISI-BERSAMPINGAN dgn web PHP yang sekarang
   (input sama, harus hasil sama) sebelum ditandai selesai.
4. Update tabel status di file ini tiap akhir sesi kerja.
