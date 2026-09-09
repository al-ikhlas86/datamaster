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
2. **Muncul 1 layar tanya "Cara pakai PC ini"** (cuma sekali, tidak akan
   muncul lagi sesudahnya) - pilih salah satu:
   - **"PC ini berdiri sendiri"** (paling umum, sudah terpilih default) -
     langsung klik Lanjutkan kalau ini satu-satunya PC di unit ini.
   - **"PC ini jadi SERVER"** - pilih HANYA di 1 PC, kalau unit ini punya
     beberapa PC yang harus melihat data yang sama (lihat bagian 4).
   - **"PC ini ikut PC lain (klien)"** - pilih di PC ke-2/ke-3 dst, isi
     alamat PC yang sudah dipilih "Server" tadi.
3. Muncul jendela splash "Cek update...", lalu jendela utama aplikasi
   (mesin Chromium/WebView2 - kalau Windows-nya belum pernah pasang
   "Microsoft Edge WebView2 Runtime", akan diminta pasang dulu sekali, ini
   normal, cukup ikuti saja).
4. Karena database masih kosong, aplikasi otomatis menampilkan halaman
   **"Setup Awal"** - isi email, username (boleh dikosongkan, defaultnya
   pakai email), dan password admin pertama (dipakai TU/admin sekolah untuk
   login sehari-hari). Di bagian bawah ada 2 kolom opsional "Alamat Hub API"
   dan "Token Hub API" - kalau belum punya token, klik link "Buat Token"
   di bawahnya (akan membuka halaman generate token, lihat bagian 2),
   generate, copy, tempel di sini - atau lewati dulu, bisa diisi belakangan
   lewat menu Pengaturan.
5. Setelah submit, langsung masuk ke Dashboard (kalau token Hub API diisi,
   aplikasi akan menyala ulang sebentar sendiri untuk mengaktifkan
   sinkronisasi - normal, tunggu beberapa detik). Instalasi selesai - siap
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

## 2. Cara generate token Hub API (buat unit baru: TK/SD/dst)

Token ini yang menghubungkan 1 instalasi Data Master ke unit sekolahnya
di server pusat (Hub API/VPS) - tanpa token, aplikasi tetap jalan normal
tapi berdiri sendiri, tidak sinkron kemana-mana. **TK dan SD masing-masing
punya token SENDIRI** - jangan pernah pakai token yang sama di 2 instalasi.

1. Buka `https://<alamat-hub-api-anda>/admin` di browser (bukan di dalam
   aplikasi Data Master - buka terpisah).
2. Masuk pakai password panel admin (disetel di server lewat
   `admin.panelPassword` di file `.env` Hub API - minta ke yang pegang
   akses server kalau belum tahu).
3. Isi "Nama Unit" (contoh: "TK Al Ikhlas 86"), klik "Generate Token".
4. Token muncul SEKALI di kotak hitam - **copy sekarang juga**, halaman
   ini tidak menyimpan/menampilkannya lagi setelah ditutup/di-refresh.
5. Tempel token itu ke kolom "Token Hub API" saat Setup Awal Data Master
   di PC unit yang bersangkutan (lihat bagian 1 langkah 2), atau lewat
   menu Pengaturan kalau instalasinya sudah lebih dulu jalan.

Unit yang sudah tidak dipakai (mis. ganti PC, ganti token krn bocor) bisa
dinonaktifkan dari halaman yang sama (tombol "Nonaktifkan") - token lama
langsung ditolak Hub API sejak saat itu, tanpa perlu ganti apapun di
Data Master (biarkan saja, kalaupun masih terisi tokennya, cuma akan
gagal sinkron - tidak error mengganggu penggunaan aplikasi sehari-hari).

---

## 3. Cara lanjut mengembangkan (mode developer)

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

## 4. Beberapa PC pakai 1 data yang sama (mis. 3 PC TU SD)

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

Pilihan Mandiri/Server/Klien ini sekarang ditanyakan lewat layar wizard
begitu `DataMaster.exe` pertama kali dibuka (lihat bagian 1 langkah 2) -
tidak perlu edit `appsettings.json` manual lagi. Urutan setup-nya:

Alamatnya pakai **NAMA PC, bukan alamat IP** - sengaja dipilih begini krn
IP bisa berubah-ubah sendiri tiap PC dinyalakan ulang (dibagikan otomatis
oleh router), sedangkan nama PC Windows TIDAK PERNAH berubah sendiri
kecuali memang sengaja diganti manual. Jadi sekali diisi nama PC saat
setup, tidak akan pernah basi walau PC dimatikan/dinyalakan berkali-kali,
beda dari kalau pakai IP.

### Setup PC server (PC 1)

1. Pasang aplikasi seperti biasa (bagian 1 di atas). Di layar wizard
   pertama, pilih **"PC ini jadi SERVER"** - layar akan langsung
   menampilkan **nama PC ini** (dicatat/screenshot, ini yang nanti diisi
   di PC klien), isi port (default 5250 sudah cukup, tidak perlu diubah
   kecuali bentrok dgn aplikasi lain), klik Lanjutkan.
2. Izinkan port ini masuk lewat Windows Firewall PC ini (Control Panel →
   Windows Defender Firewall → Pengaturan lanjutan → Inbound Rules → New
   Rule → Port → TCP 5250 → Allow). Tanpa ini PC lain akan gagal konek
   walau sudah 1 jaringan.
3. Pastikan "Network Discovery" menyala di PC ini (Control Panel →
   Network and Sharing Center → Change advanced sharing settings → Turn
   on network discovery) - ini yang membuat PC lain di jaringan yang sama
   bisa menemukan PC ini lewat namanya, biasanya sudah menyala default
   di jaringan rumah/kantor kecil (profil "Private"), jarang perlu diubah.

### Setup PC klien (PC 2, PC 3, dst)

1. Pasang aplikasi seperti biasa (bagian 1 di atas). Di layar wizard
   pertama, pilih **"PC ini ikut PC lain (klien)"**, isi alamat PC server
   (NAMA PC yang tampil di layar server tadi + port-nya, contoh
   `http://NAMA-PC-SERVER:5250`), klik Lanjutkan.
2. Selesai - jendelanya langsung menampilkan data dari PC server, seolah-
   olah memakai aplikasi yang sama persis. Tidak perlu isi Setup Awal
   sendiri (PC ini tidak punya database sendiri).

**Kalau PC klien tidak bisa konek pakai nama PC** (jarang terjadi, biasanya
krn "Network Discovery" mati atau jaringan kantor pakai pengaturan
khusus): sebagai jalan pintas, boleh pakai alamat IP PC server sbg
gantinya (`ipconfig` di PC server, baris "IPv4 Address") - tapi minta
staf jaringan mereservasi IP itu di router (DHCP reservation) supaya
tidak berubah-ubah, karena IP TIDAK punya jaminan permanen seperti nama PC.

Update otomatis (token GitHub, bagian 1 langkah 3) tetap perlu diisi di
SEMUA PC (server maupun klien) - masing-masing tetap punya salinan
program sendiri yang perlu diperbarui, cuma DATA-nya yang dipusatkan di
PC server.

**Kalau wizard sudah kelewat/salah pilih**: hapus baris `"SetupSelesai":
true` dari `appsettings.json` sebelah `DataMaster.exe` (atau ubah jadi
`false`), buka lagi aplikasinya - wizard akan muncul lagi.

---

## 5. Migrasi PC TU SD (alat sudah siap & teruji, BELUM dijalankan ke data asli)

PC TU SD sekarang masih pakai versi web PHP
(`C:\xampp\htdocs\webarsipdata-master`, jalan lewat XAMPP + MySQL) dan
**akan tetap begitu** sampai versi desktop C# ini terbukti stabil 100% di
PC TU TK dulu (unit baru, tanpa data lama, jadi aman dites langsung dengan
data sungguhan tanpa risiko) - baru setelah itu langkah di bawah ini
dijalankan sungguhan ke data PC TU SD.

Alat migrasinya (`src/DataMaster.MigrationTool`, folder develop) sudah
dibangun & sudah diuji nyata (bukan cuma dibaca kodenya) - dijalankan ke
salinan database MySQL PHP di laptop develop, hasilnya **100% cocok di
SEMUA 19 tabel data sekolah** (termasuk 2.300 baris Jadwal Pelajaran),
termasuk verifikasi isi datanya (bukan cuma jumlah baris) dan seluruh
relasi antar tabel (Wali Kelas, Guru Pengampu, dst) tetap benar setelah
ID-nya diterjemahkan ulang.

**Sengaja TIDAK memindahkan**: tabel `users` (akun login) - password lama
PHP tidak bisa dipakai di aplikasi baru (algoritma beda), jadi akun dibuat
ulang lewat Setup Awal setelah migrasi (lihat langkah 4). `system_settings`
juga tidak dipindah - itu murni pengaturan operasional, bukan data sekolah.

Langkah migrasi PC TU SD sungguhan (dilakukan HANYA setelah PC TU TK
terbukti aman berjalan beberapa waktu):

1. Backup database MySQL PC TU SD (`mysqldump webarsipdata_db > backup.sql`
   atau lewat phpMyAdmin - Export). Simpan salinan ini terpisah, JANGAN
   dihapus sampai migrasi benar-benar dipastikan sukses.
2. Pasang salinan backup itu ke MySQL manapun yang bisa diakses alat
   migrasi (boleh di PC TU SD itu sendiri, atau di komputer developer -
   yang penting BUKAN database yang sedang dipakai live, pakai salinannya).
3. Jalankan alat migrasi dari folder develop:
   ```
   cd "D:\Data Master"
   dotnet run --project src/DataMaster.MigrationTool -- --mysql "Server=localhost;Database=webarsipdata_db;User=root;Password=xxx;" --sqlite "C:\lokasi\datamaster_sd.db"
   ```
   Baca LAPORAN HASIL MIGRASI di akhir - semua baris harus "OK" (jumlah
   SQLite = MySQL persis). Kalau ada yang "dilewati", baca peringatan di
   atasnya sebelum lanjut - JANGAN pakai hasilnya kalau ada yang janggal.
4. Salin folder dokumen upload PHP (foto/KK/akta siswa, dst) ke
   `App_Data/uploads/dokumen` instalasi desktop - alat migrasi cuma
   memindahkan NAMA file di database, bukan berkas fisiknya.
5. Pasang aplikasi desktop di PC TU SD (langkah sama seperti bagian 1),
   tapi PAKAI file `datamaster_sd.db` hasil migrasi tadi sebagai databasenya
   (timpa `App_Data/datamaster.db` yang kosong bawaan instalasi baru).
6. Buka aplikasinya - karena tabel Users sengaja tidak ikut migrasi, akan
   muncul halaman Setup Awal seperti PC baru - buat akun admin dengan
   password baru.
7. Cek visual satu-persatu di menu Kepala Sekolah, Guru Pengampu, dan Wali
   Kelas - datanya sudah pasti benar (sudah diverifikasi), tapi tetap
   patut dilihat langsung sebelum benar-benar dipakai sehari-hari.
8. Baru setelah semua dicek dan TU merasa yakin, web PHP di PC TU SD
   dimatikan dan TU mulai pakai aplikasi desktop sepenuhnya.

Jangan hapus/matikan web PHP PC TU SD sebelum langkah 7-8 selesai dan
sudah dipastikan aman. Backup dari langkah 1 disimpan sampai yakin migrasi
sukses total, baru boleh dihapus.

---

## 6. Pertanyaan yang sering muncul

**Fitur "Pulihkan Data" di menu Setting - apakah bisa dipakai menarik data
dari web PHP yang lama?** Tidak. "Pulihkan Data" (restore) HANYA bisa
membaca file cadangan yang dibuat SENDIRI oleh aplikasi desktop ini
(tombol "Backup Sekarang" di menu yang sama, formatnya `.db` bawaan
SQLite) - dia tidak tahu apa-apa soal database MySQL web PHP yang lama.
Memindahkan data dari web PHP ke sini adalah proses TERPISAH lewat alat
migrasi khusus (lihat bagian 5), bukan lewat tombol Pulihkan Data ini.

**Kenapa saat instalasi tidak ada pilihan "Pendidikan/Perusahaan/Develop"?**
"Pendidikan" vs "Perusahaan" itu 1 pengaturan (`AppSettings:InstallType`
di `appsettings.json` sebelah `DataMaster.Web.exe`, bukan sebelah
`DataMaster.exe`), bukan sesuatu yang ditanyakan pas instalasi - untuk
sekolah ini nilainya SELALU "pendidikan" (default), tidak perlu diubah.
"Develop" bukan pilihan instalasi sama sekali - itu artinya menjalankan
KODE SUMBER dari folder `D:\Data Master` (lihat bagian 3), sepenuhnya
terpisah dari file `.zip` hasil download di bagian 1. "Mandiri/Server/
Klien" MEMANG ditanyakan pas instalasi (wizard di bagian 1 langkah 2) -
itu beda konsep, murni soal apakah PC ini pakai data sendiri atau ikut
PC lain di jaringan yang sama, tidak ada hubungannya dgn Pendidikan/
Perusahaan.

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

## 7. Kontak & eskalasi

Kalau ada error yang tidak dimengerti: catat langkah persis yang bikin
error muncul, screenshot pesan errornya (kalau ada), lalu cek dulu apakah
sudah pernah ditulis di `spec/00-INDEX.md` (bagian "Catatan modul" tiap
file spek biasanya berisi bug yang pernah ditemukan & cara perbaikannya).
Kalau belum ada catatannya, itu berarti bug baru - laporkan ke developer
sebelumnya dengan detail selengkap mungkin (langkah reproduksi, data yang
dipakai, screenshot).
