# Data Master (Desktop)

Port 1:1 dari [`datamaster`](https://github.com/al-ikhlas86/datamaster) (PHP CodeIgniter 4,
dipakai PC TU SD sekarang) ke aplikasi desktop `.exe` mandiri - **tanpa
XAMPP/Apache/PHP/MySQL** sama sekali di PC target.

**Status: dalam pembangunan, BELUM dipakai produksi.** Web PHP di PC TU SD
tetap berjalan 100% seperti biasa dan TIDAK disentuh oleh proyek ini. Rencana
peluncuran: uji coba dulu sebagai instalasi **PC TU TK** (unit baru, tanpa
data lama yang perlu dimigrasikan) - baru setelah terbukti 100% stabil &
identik perilakunya, data PC TU SD diimpor ke sini menggantikan web PHP.

## Folder develop vs folder download

Dua hal ini sering ketuker, jadi ditulis eksplisit:

- **Develop (ubah kode, di sini)**: folder ini sendiri, `D:\Data Master`.
  Berisi source code C#, bukan sesuatu yang dipasang ke PC sekolah.
- **Download (pasang ke PC sekolah)**: halaman Releases repo GitHub ini -
  https://github.com/al-ikhlas86/datamaster-desktop/releases - unduh
  `DataMaster-win-x64.zip` dari rilis terbaru, extract, jalankan
  `DataMaster.exe`. Repo ini privat, jadi perlu akun GitHub yang sudah
  diundang sbg kolaborator untuk bisa mengunduh.

Panduan lengkap langkah demi langkah (instalasi PC sekolah, aktifkan update
otomatis, cara lanjut develop, rencana migrasi PC TU SD) ada di
**[`PANDUAN-INSTALASI.md`](PANDUAN-INSTALASI.md)** - baca itu duluan kalau
ini pertama kalinya pegang project ini.

## Kenapa arsitektur ini (bukan tulis ulang jadi WinForms/WPF native murni)

Data Master itu aplikasi CRUD berat (puluhan tabel, form, validasi, cetak,
import Excel) - jauh lebih kompleks dari `Presensi.exe` (klien tipis kamera
tanpa database sendiri). Menulis ulang SEMUA layar jadi native WinForms/WPF
akan menghabiskan waktu berkali lipat DAN sulit menjamin tampilan "100% sama
persis" (WinForms/WPF tidak bisa meniru layout HTML/CSS/Bootstrap yang
sekarang tanpa upaya custom-draw besar).

**Pilihan arsitektur**: pertahankan pola MVC + halaman web (paling mirip
CodeIgniter 4 - Controller↔Controller, View(.php)↔View(.cshtml),
Model↔Entity - meminimalkan risiko "salah terjemah" perilaku), tapi jalankan
semuanya 100% lokal tanpa server terpisah:

- **ASP.NET Core MVC** (.NET, C#) - menjalankan server webnya SENDIRI
  (Kestrel bawaan .NET) - tidak perlu Apache/Nginx terpisah sama sekali.
- **Entity Framework Core + SQLite** - database jadi 1 file tunggal
  (`datamaster.db`), tidak perlu instalasi MySQL/MariaDB terpisah, tidak ada
  service Windows yang perlu dikelola.
- **Publish self-contained single-file** - hasil akhirnya 1 file `.exe`
  yang SUDAH termasuk .NET runtime di dalamnya - PC target tidak perlu
  instal apapun sama sekali, cukup jalankan.
- **Launcher (WPF) + WebView2** - jendela native (bukan tab browser biasa)
  yang menjalankan server di latar belakang lalu menampilkan halamannya di
  jendela sendiri (ikon taskbar sendiri, terasa seperti aplikasi asli) -
  WebView2 pakai mesin Chromium yang sama dengan Edge modern, jadi CSS/JS
  yang sama dgn web PHP sekarang akan tampil identik.
- **Auto-update ala Presensi.exe** - `Launcher` cek rilis terbaru dari
  GitHub Releases repo ini (privat, token fine-grained "Contents: Read-only"
  KHUSUS repo ini, pola PERSIS `Presensi/Services/UpdateService.cs`), unduh,
  tutup app, timpa file, buka lagi - otomatis, tanpa installer.

## Struktur proyek

```
DataMaster.sln
src/
  DataMaster.Data/      - Entity Framework Core: DbContext + entities
                           (port dari app/Models + app/Database/Migrations
                           PHP), SQLite provider.
  DataMaster.Web/        - ASP.NET Core MVC: seluruh halaman/logic bisnis
                           (port dari app/Controllers + app/Views PHP).
                           Kestrel bind ke 127.0.0.1 default; opsi
                           "izinkan jaringan yang sama" (Pengaturan) bind ke
                           0.0.0.0 supaya PC lain di jaringan sama bisa akses.
  DataMaster.Launcher/   - WPF: splash "Cek update...", start/stop proses
                           DataMaster.Web di background, jendela WebView2,
                           auto-updater (GitHub Releases).
spec/                    - Dokumen inventarisasi LENGKAP perilaku PHP yang
                           sudah ada (per modul) - acuan wajib supaya tidak
                           ada satu detail pun terlewat saat membangun ulang.
                           Baca SEBELUM menulis kode modul terkait.
```

## Sinkronisasi ke ekosistem (Hub API / Mobile App / Webview App)

TIDAK BERUBAH secara konsep dari versi PHP - Hub API (VPS) sepenuhnya
agnostik terhadap siapa yang mengirim data, selama bentuk JSON-nya sama
persis. Logic `SyncPush.php` di-port jadi service C# (`HttpClient` ke
endpoint Hub API yang SAMA), dijadwalkan jalan tiap siklus (mis. tiap menit,
sama seperti Task Scheduler `WebArsipData SyncPush` di versi PHP) - lihat
`spec/04-infra-auth-sync.md` untuk detail lengkap tiap entitas yang
disinkronkan.

**Unit ini terindeks sbg TU TK** (`unit_id` terpisah dari SD di Hub API) -
data tidak akan pernah bercampur/menimpa data SD di server manapun, aman
dipakai bersamaan dgn PC TU SD yang masih berjalan di web PHP.

## Status pembangunan

Lihat `spec/00-INDEX.md` untuk peta lengkap progres per modul.
