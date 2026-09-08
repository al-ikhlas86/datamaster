# Indeks Spesifikasi & Progres Pembangunan

Dokumen ini adalah peta status - baca ini dulu sebelum mulai kerja di sesi mana pun,
supaya tahu persis sudah sampai mana dan apa langkah berikutnya.

## Status Inventarisasi (baca kode PHP, TANPA mengubah apapun)

| # | Modul | File spec | Status |
|---|---|---|---|
| 1 | Siswa & PSB (Calon Siswa) | `01-siswa-psb.md` | Sedang dikerjakan |
| 2 | Guru, Penugasan Mengajar, Kepala Sekolah, Kelas, Tahun Ajaran, Ekskul | `02-guru-kelas-struktur.md` | Sedang dikerjakan |
| 3 | Kurikulum, Jadwal Pelajaran, Kalender Akademik, Akademik | `03-akademik-jadwal.md` | Sedang dikerjakan |
| 4 | Auth, Manajemen Pengguna, Dashboard, Sinkronisasi Hub API, Backup | `04-infra-auth-sync.md` | Sedang dikerjakan |

## Status Pembangunan .NET

| Bagian | Status |
|---|---|
| Solusi + 3 proyek (Data/Web/Launcher) discaffold, build sukses | Selesai |
| Skema database (EF Core entities) | Belum dimulai - menunggu spec #1-4 |
| Halaman/Controller per modul | Belum dimulai |
| Sinkronisasi Hub API (port dari SyncPush.php) | Belum dimulai |
| Launcher (splash, start/stop server, WebView2, auto-update) | Belum dimulai |
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
