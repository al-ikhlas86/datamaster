# Panduan Instalasi & Serah Terima - Data Master Desktop

Dokumen ini untuk staf IT yang akan melanjutkan/merawat aplikasi ini setelah
tahap pengembangan awal selesai. Ditulis supaya orang yang belum pernah
pegang project ini sama sekali tetap bisa jalan tanpa nanya-nanya lagi.

Ada 2 folder yang perlu dibedakan sejak awal:

| | Lokasi | Isinya | Dipakai untuk |
|---|---|---|---|
| **Folder DEVELOP** | `D:\Data Master` (source code lengkap) | Kode sumber C#, project Visual Studio/`.slnx`, dokumen spek `spec/` | Ubah/tambah fitur, build ulang, testing |
| **Folder/tempat DOWNLOAD** | GitHub Releases repo `al-ikhlas86/datamaster-desktop` → https://github.com/al-ikhlas86/datamaster-desktop/releases | File `.zip` siap pakai hasil build otomatis | Instalasi ke PC sekolah (PC TU TK, lalu PC TU SD) |

Singkatnya: **kalau mau ngoprek/ubah kode → buka folder `D:\Data Master`**.
**Kalau mau pasang aplikasi ke PC sekolah → download dari halaman Releases
GitHub, bukan dari folder `D:\Data Master`.** Folder develop TIDAK dipasang
langsung ke PC sekolah - dia perlu di-build dulu (baik oleh GitHub Actions
otomatis, atau manual kalau kepepet, lihat bagian bawah).

---

## 1. Cara pasang aplikasi di PC sekolah (dari nol)

Repo `datamaster-desktop` ini **privat** (bukan publik), jadi cuma akun
GitHub yang sudah diundang sebagai kolaborator yang bisa mengunduh rilisnya.
Kalau staf IT belum punya akses, minta ditambahkan dulu sebagai kolaborator
di GitHub oleh pemilik akun `al-ikhlas86`.

### Langkah 1 - Unduh paket instalasi

1. Buka https://github.com/al-ikhlas86/datamaster-desktop/releases (harus
   login GitHub dulu dengan akun yang sudah diundang).
2. Klik rilis paling atas (nomor versi terbesar, misal `v1.0.0`).
3. Di bagian "Assets", unduh file **`DataMaster-win-x64.zip`**.
4. Extract ke folder permanen di PC sekolah, contoh: `D:\DataMaster` (bebas,
   yang penting jangan di Desktop/Downloads yang gampang kehapus, dan jangan
   di dalam folder yang butuh hak admin khusus seperti `C:\Program Files`).

Isi setelah di-extract kira-kira begini:

```
D:\DataMaster\
  DataMaster.exe          <- ini yang dijalankan (Launcher)
  DataMaster.Launcher.dll, dll runtime .NET lainnya
  web\
    DataMaster.Web.exe    <- server internal, dijalankan OTOMATIS oleh
                              DataMaster.exe, JANGAN dijalankan manual
```

### Langkah 2 - Jalankan pertama kali

1. Dobel-klik `DataMaster.exe`.
2. Akan muncul jendela splash "Cek update...", lalu jendela utama aplikasi
   (mesin Chromium/WebView2 - kalau Windows-nya belum pernah pasang
   "Microsoft Edge WebView2 Runtime", akan diminta pasang dulu sekali, ini
   normal, cukup ikuti saja).
3. Karena database masih kosong, aplikasi otomatis menampilkan halaman
   **"Setup Awal"** - isi email, username (boleh dikosongkan, defaultnya
   pakai email), dan password admin pertama. Password ini yang dipakai
   TU/admin sekolah untuk login sehari-hari.
4. Setelah submit, langsung masuk ke Dashboard. Instalasi selesai - siap
   diisi data sekolah (Tahun Ajaran, Kelas, Guru, Siswa, dst, urutannya
   sama seperti versi web PHP yang sekarang dipakai PC TU SD).

### Langkah 3 - Aktifkan update otomatis (opsional tapi sangat disarankan)

Tanpa langkah ini aplikasi tetap jalan normal, cuma tidak akan pernah cek
rilis baru sendiri (harus update manual/unduh ulang tiap ada perbaikan).

1. Buka https://github.com/settings/tokens?type=beta (harus login akun
   GitHub yang sudah diundang ke repo).
2. "Generate new token" (Fine-grained token):
   - **Repository access**: pilih "Only select repositories" → pilih
     `al-ikhlas86/datamaster-desktop` SAJA (jangan kasih akses ke repo
     lain, prinsip izin seminimal mungkin).
   - **Permissions**: `Contents` → set ke **Read-only**. Yang lain biarkan
     "No access".
   - Masa berlaku: pilih yang paling panjang tersedia (atau "No expiration"
     kalau ada), supaya tidak perlu diulang tiap beberapa bulan.
3. Generate, salin tokennya (formatnya diawali `github_pat_...`, cuma
   ditampilkan SEKALI, kalau kelewat harus generate baru).
4. Buka file `appsettings.json` di folder yang sama dengan `DataMaster.exe`
   (kalau belum ada, jalankan `DataMaster.exe` sekali dulu - file ini
   otomatis dibuat kosong saat aplikasi pertama kali start).
5. Isi seperti ini, lalu simpan:
   ```json
   {
     "GithubToken": "github_pat_xxxxxxxxxxxxxxxxxxxxx"
   }
   ```
6. Tutup & buka lagi `DataMaster.exe`. Mulai sekarang, tiap ada rilis baru
   di GitHub, aplikasi akan otomatis mengunduh & memasang sendiri saat
   dibuka (proses: cek versi → unduh di latar belakang → tutup server
   internal → timpa file lama → buka lagi otomatis - tanpa perlu
   uninstall/reinstall manual, tanpa installer, tanpa campur tangan orang
   sekolah).

**Token ini JANGAN pernah disebar/di-commit ke git** - dia cuma bisa
*membaca* rilis repo ini (tidak bisa ubah kode/data apapun), tapi tetap
kredensial pribadi, perlakukan seperti password.

---

## 2. Cara lanjut mengembangkan (mode developer)

Semua kode ada di folder **`D:\Data Master`** (bukan di PC sekolah - ini
folder kerja di komputer developer/IT).

### Yang perlu terpasang di komputer developer

- **.NET 10 SDK** (bukan cuma Runtime) - unduh dari
  https://dotnet.microsoft.com/download/dotnet/10.0
- **Visual Studio 2022** (edisi Community gratis juga cukup) dengan workload
  ".NET desktop development" - atau kalau lebih suka command line, cukup
  .NET SDK saja + editor teks apapun (VS Code, dsb).
- **Git**, sudah login ke akun GitHub yang punya akses tulis ke repo
  `al-ikhlas86/datamaster-desktop`.

### Kalau belum punya folder kodenya sama sekali

Ini beda dengan bagian 1 di atas - di sini yang diambil adalah KODE SUMBER,
bukan file `.zip` siap-pasang dari halaman Releases (zip Releases tidak
bisa dibuka/diubah sebagai kode).

```
git clone https://github.com/al-ikhlas86/datamaster-desktop.git "D:\Data Master"
```

Butuh login GitHub (akun yang sudah diundang sbg kolaborator repo). Kalau
tidak familiar dengan Git command line, cara termudah: buka halaman
https://github.com/al-ikhlas86/datamaster-desktop di browser → tombol hijau
"Code" → "Open with GitHub Desktop" (aplikasi GitHub Desktop, gratis,
lebih ramah pemula) atau "Download ZIP" (tapi ini TIDAK ideal utk
develop lanjutan krn tidak tersambung ke riwayat git - `git clone`/GitHub
Desktop lebih disarankan).

### Menjalankan versi developer di komputer sendiri

```
cd "D:\Data Master"
dotnet run --project src/DataMaster.Web
```

Ini menjalankan server web-nya saja (tanpa jendela Launcher/WebView2) -
buka `http://localhost:5000` (atau port yang tertulis di layar) lewat
browser biasa buat ngetes cepat. Database dev otomatis dibuat di
`src/DataMaster.Web/App_Data/datamaster.db` (file SQLite, bisa dibuka pakai
DB Browser for SQLite kalau perlu intip isinya langsung).

Untuk menjalankan PERSIS seperti pengalaman PC sekolah (lewat jendela
Launcher + WebView2, bukan browser biasa), jalankan lewat Visual Studio:
buka `DataMaster.slnx`, set `DataMaster.Launcher` sebagai Startup Project,
tekan F5.

### Alur setelah mengubah kode

1. Ubah kode di `src/DataMaster.Web` (tampilan/logic) atau
   `src/DataMaster.Data` (struktur database/entity).
2. Test lokal dulu (`dotnet run` seperti di atas, atau F5 di Visual Studio).
3. `git add`, `git commit`, `git push` ke branch `main`.
4. Push commit BIASA **tidak** memicu build rilis - supaya tidak numpuk
   rilis tiap commit kecil. Build rilis baru jalan kalau ada **git tag**
   berawalan `v` yang di-push, misal:
   ```
   git tag v1.0.1
   git push origin v1.0.1
   ```
5. Begitu tag ke-push, GitHub Actions otomatis: compile, bikin paket zip,
   terbitkan sebagai Release baru di halaman Releases. Prosesnya bisa
   dipantau di tab "Actions" repo GitHub (biasanya selesai dalam beberapa
   menit). Setelah itu, PC sekolah yang sudah diisi token (lihat bagian 1
   langkah 3) otomatis menawarkan update begitu aplikasi dibuka.
6. Kalau cuma mau coba build tanpa bikin rilis resmi (misal ngetes apakah
   compile-nya sukses di lingkungan CI), jalankan workflow secara manual
   dari tab Actions → "Build Data Master" → "Run workflow" (tidak perlu
   bikin tag).

### Dokumen pendukung wajib dibaca sebelum ubah modul tertentu

Folder `spec/` berisi catatan detail perilaku versi PHP asli per modul,
dipakai supaya port ke C# tidak meleset satu titik pun dari aslinya:

- `spec/00-INDEX.md` - peta status tiap modul, mana yang sudah/belum
  dikerjakan, catatan bug yang pernah ditemukan & cara perbaikannya.
- `spec/01-siswa-psb.md`, `spec/02-guru-kelas-struktur.md`,
  `spec/03-akademik-jadwal.md`, `spec/04-infra-auth-sync.md` - detail
  per area modul.

Kalau mau ubah modul Siswa misalnya, baca dulu bagian terkait di
`spec/01-siswa-psb.md` DAN buka langsung kode PHP aslinya di
`C:\xampp\htdocs\webarsipdata-master\app\Controllers\Siswa.php` /
`app\Views\siswa\` sebagai pembanding - jangan cuma modif dari feeling,
karena tujuan proyek ini adalah 100% sama persis dengan yang lama.

---

## 3. Beberapa PC pakai 1 data yang sama (mis. 3 PC TU SD)

Kalau cuma 1 PC per unit (kasus PC TU TK sekarang), LEWATI bagian ini -
instalasi biasa di bagian 1 di atas sudah cukup, tiap PC otomatis berdiri
sendiri ("mode mandiri" - server & database dia sendiri).

Kalau ada BEBERAPA PC yang harus melihat/mengubah data YANG SAMA PERSIS
(bukan salinan masing-masing - beneran 1 database yang sama, real-time,
tanpa perlu internet, cukup 1 jaringan lokal/WiFi/kabel yang sama), pakai
mode **server + klien**:

- **1 PC jadi "server"** - PC inilah yang benar-benar menyimpan database.
  Sebaiknya PC yang paling jarang dimatikan/direstart (PC 1 harus MENYALA &
  aplikasinya TERBUKA setiap kali PC 2/3 mau dipakai - kalau PC 1 mati, PC
  2/3 tidak bisa apa-apa).
- **PC lainnya jadi "klien"** - jendela aplikasi mereka LANGSUNG menampilkan
  apa yang ada di PC server, tanpa database sendiri sama sekali. Karena
  benar-benar 1 database yang sama, akun login JUGA otomatis sama untuk
  semua PC (tidak perlu diatur ulang per PC) - siapa saja yang punya
  akun bisa login dari PC mana saja di antara ketiganya.

### Setup PC server (PC 1)

1. Pasang aplikasi seperti biasa (bagian 1 di atas).
2. Cari tahu IP lokal PC ini di jaringan kantor (`ipconfig` di Command
   Prompt, lihat baris "IPv4 Address", contoh `192.168.1.10`). **Minta staf
   jaringan mereservasi IP ini di router (DHCP reservation)** supaya
   alamatnya tidak berubah-ubah tiap PC dinyalakan ulang - kalau berubah,
   PC klien akan kehilangan koneksi sampai alamatnya disetel ulang manual.
3. Buka `appsettings.json` di sebelah `DataMaster.exe`, isi:
   ```json
   {
     "Mode": "server",
     "ServerPort": 5250
   }
   ```
4. Izinkan port ini masuk lewat Windows Firewall PC ini (Control Panel →
   Windows Defender Firewall → Pengaturan lanjutan → Inbound Rules → New
   Rule → Port → TCP 5250 → Allow). Tanpa ini PC lain akan gagal konek
   walau sudah 1 jaringan.
5. Tutup & buka lagi `DataMaster.exe`.

### Setup PC klien (PC 2, PC 3, dst)

1. Pasang aplikasi seperti biasa (bagian 1 di atas) - TAPI setelah dibuka
   pertama kali, JANGAN isi Setup Awal (tidak akan pernah dipakai, PC ini
   tidak punya database sendiri). Tutup aplikasinya.
2. Buka `appsettings.json` di sebelah `DataMaster.exe` PC ini, isi (ganti
   IP sesuai IP PC server yang dicatat di atas):
   ```json
   {
     "Mode": "klien",
     "KlienServerUrl": "http://192.168.1.10:5250"
   }
   ```
3. Buka lagi `DataMaster.exe` - sekarang jendelanya langsung menampilkan
   data dari PC server, seolah-olah memakai aplikasi yang sama persis.

Update otomatis (token GitHub, bagian 1 langkah 3) tetap perlu diisi di
SEMUA PC (server maupun klien) - masing-masing tetap punya salinan
program sendiri yang perlu diperbarui, cuma DATA-nya yang dipusatkan di
PC server.

---

## 4. Rencana migrasi PC TU SD (tahap berikutnya, BELUM dikerjakan)

PC TU SD sekarang masih pakai versi web PHP
(`C:\xampp\htdocs\webarsipdata-master`, jalan lewat XAMPP + MySQL) dan
**akan tetap begitu** sampai versi desktop C# ini terbukti stabil 100% di
PC TU TK dulu (unit baru, tanpa data lama, jadi aman dites langsung dengan
data sungguhan tanpa risiko).

Setelah PC TU TK terbukti aman berjalan beberapa waktu, langkah pemindahan
PC TU SD adalah:

1. Bikin alat migrasi satu-arah: baca database MySQL PHP yang sekarang
   dipakai PC TU SD (30 tabel, termasuk siswa/guru/kelas/jadwal/kurikulum/
   dst), tulis ulang ke database SQLite aplikasi desktop ini - alat ini
   BELUM dibuat, jadi jangan mulai proses pemindahan data PC TU SD sebelum
   alat ini ada & sudah dites dengan salinan data (bukan langsung ke data
   asli).
2. Install aplikasi desktop ini di PC TU SD (langkah sama seperti bagian 1
   di atas, tapi database masih kosong).
3. Jalankan alat migrasi SEKALI, verifikasi jumlah baris & isi data cocok
   antara MySQL lama vs SQLite baru untuk tiap tabel.
4. Baru setelah dicek benar-benar cocok, web PHP di PC TU SD dimatikan dan
   TU mulai pakai aplikasi desktop sepenuhnya.

Jangan hapus/matikan web PHP PC TU SD sebelum langkah 3-4 selesai dan
sudah dipastikan aman.

---

## 5. Pertanyaan yang sering muncul

**Fitur "Pulihkan Data" di menu Setting - apakah bisa dipakai menarik data
dari web PHP yang lama?** Tidak. "Pulihkan Data" (restore) HANYA bisa
membaca file cadangan yang dibuat SENDIRI oleh aplikasi desktop ini
(tombol "Backup Sekarang" di menu yang sama, formatnya `.db` bawaan
SQLite) - dia tidak tahu apa-apa soal database MySQL web PHP yang lama.
Memindahkan data dari web PHP ke sini adalah proses TERPISAH (lihat
bagian 4 di bawah, "alat migrasi" yang belum dibuat), bukan lewat tombol
Pulihkan Data ini.

**Kenapa saat instalasi tidak ada pilihan "Pendidikan/Perusahaan/Develop"?**
"Pendidikan" vs "Perusahaan" itu 1 pengaturan (`AppSettings:InstallType`
di `appsettings.json` sebelah `DataMaster.Web.exe`, bukan sebelah
`DataMaster.exe`), bukan sesuatu yang ditanyakan pas instalasi - untuk
sekolah ini nilainya SELALU "pendidikan" (default), tidak perlu diubah.
"Develop" bukan pilihan instalasi sama sekali - itu artinya menjalankan
KODE SUMBER dari folder `D:\Data Master` (lihat bagian 2), sepenuhnya
terpisah dari file `.zip` hasil download di bagian 1.

**Bagaimana Hub API (server pusat di VPS) tahu data yang masuk itu dari
unit TK atau unit SD?** Bukan dari isi datanya, tapi dari TOKEN yang
dipakai tiap instalasi utk mengirim data. Tiap unit (TK, SD, dst) didaftarkan
sbg 1 baris "klien API" terpisah di server Hub API (`php spark
api:create-client "Nama Unit" source <unit_id>`), masing-masing dapat token
unik sendiri yang disetel di `AppSettings:HubApiToken` (`appsettings.json`
sebelah `DataMaster.Web.exe`) instalasi bersangkutan. Begitu Data Master
mengirim data pakai token TK, Hub API otomatis tahu itu milik unit TK
(dari token-nya, bukan ditebak dari isi datanya) - data TK dan SD tidak
akan pernah tercampur di server pusat selama tokennya beda.

---

## 6. Kontak & eskalasi

Kalau ada error yang tidak dimengerti: catat langkah persis yang bikin
error muncul, screenshot pesan errornya (kalau ada), lalu cek dulu apakah
sudah pernah ditulis di `spec/00-INDEX.md` (bagian "Catatan modul" tiap
file spek biasanya berisi bug yang pernah ditemukan & cara perbaikannya).
Kalau belum ada catatannya, itu berarti bug baru - laporkan ke developer
sebelumnya dengan detail selengkap mungkin (langkah reproduksi, data yang
dipakai, screenshot).
