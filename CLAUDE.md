# Data Master — Ekosistem Al-Ikhlas 86

Repo PUBLIK: jangan menulis rahasia, token, atau detail infrastruktur di sini. Konteks ekosistem lengkap ada di laptop dev pemilik: `C:\xampp\htdocs\webarsipdata-master\CLAUDE.md` dan `Projek.md`. Balas Bahasa Indonesia; commit tanpa trailer Claude.

## Isi repo (.NET 10, win-x64 self-contained)
- `src/DataMaster.Launcher` — WPF + WebView2: wizard setup, `UpdateChecker` (auto-update saat dibuka), `ServerProcessManager`, `WindowsServiceHelper`.
- `src/DataMaster.Web` — ASP.NET Core (MVC/Razor) + SQLite; data sekolah, sinkron ke Hub API (`HubApiSyncService`), backup terenkripsi.
- `src/DataMaster.Data` — EF Core entitas/migrasi.
- Versi dicap dari tag saat build (`-p:Version=`); `Services/AppVersionInfo.cs` membacanya (tampil di menu samping, dilaporkan ke Hub lewat header `X-App-Version`).

## Mode & update
- Mode server berjalan sebagai Windows Service `DataMasterWebService` (akun SYSTEM; env lewat registri; instalasi di `<folder>\web\DataMaster.Web.exe`). `ServiceAutoUpdateHostedService` memeriksa rilis tiap 3 jam dan memasang di jendela 22:00-05:00 (cadangan + pemulihan otomatis; log di `%ProgramData%\DataMaster\update`). Mati: env `AppSettings__AutoUpdateServer=false`.
- Mode development = sandbox terpisah (`DataMaster-Dev`), tanpa sinkron ke Hub produksi.
- Nama repo rilis: `al-ikhlas86/datamaster` (nama lama `datamaster-desktop` hanya hidup lewat redirect).

## Aturan
- **Jangan compile/publish lokal.** Cek kompilasi: `gh workflow run build.yml -R al-ikhlas86/datamaster`; rilis: tag `vX.Y.Z` (CI membuat Release `DataMaster-win-x64.zip`, lalu semua PC ter-update sendiri). Tag hanya setelah build uji hijau.
- Perubahan yang menyentuh format sinkron harus menaikkan `ProtocolVersion` agar PC mengirim ulang sekali.
- Skrip PowerShell/Windows Service diuji di folder tiruan dulu; jangan menyentuh layanan asli di laptop.
- Data Master ADALAH sumber valid data siswa/pegawai/jadwal; jangan membuat jalur yang menimpa data dari luar.
