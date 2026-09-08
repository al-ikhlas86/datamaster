# 02 — Guru/Pegawai, Penugasan Mengajar, Kepala Sekolah, Kelas, Tahun Ajaran, Ekskul

Sumber: `C:\xampp\htdocs\webarsipdata-master` (CodeIgniter 4, PHP), dibaca tuntas per 2026-09-08. Tujuan: replikasi 100% identik ke .NET 8/C# + SQLite.

## 0. Konsep lintas-modul yang WAJIB dipahami dulu

- **`effective_install_type()`** (`app/Common.php`): mengembalikan `'pendidikan'` atau `'perusahaan'`. Dari `env('install_type', 'pendidikan')`; kalau nilainya `'pengembang'`, tampilan efektif ikut `session('dev_preview_type')` (default `'pendidikan'`). Semua controller di modul ini bercabang berdasar `effective_install_type() === 'perusahaan'` (disebut `isPerusahaan()`/`allowedJabatan()`).
  - Perusahaan: guru dibatasi HANYA jabatan `karyawan`. Modul Kelas/Ekskul/Tahun Ajaran/PenugasanMengajar/KepalaSekolah **seluruhnya diblokir** via `PendidikanFilter` (redirect ke `/dashboard` dengan pesan error `'Modul ini tidak tersedia untuk tipe instalasi saat ini (Perusahaan).'`) kalau bukan tipe `pendidikan`.
- **Guru BUKAN dihapus fisik**. "Hapus" = set `status_aktif=0` (arsip, bisa dipulihkan). Tidak ada hard delete di jalur manapun untuk guru.
- **Jabatan guru** (`enum('guru_kelas','guru_bidang','karyawan')`) untuk `guru_kelas`/`guru_bidang` TIDAK PERNAH diketik manual sejak 2026-08-27 — otomatis derivasi dari apakah guru itu SEDANG jadi wali kelas (lihat `WaliKelasModel::sinkronJabatanGuru()`). Form/import cuma menawarkan 2 pilihan: "Guru" atau "Karyawan"; "Guru" baru selalu mulai sebagai `guru_bidang`.
- **Wali Kelas & Kepala Sekolah VERSIONED PER TAHUN AJARAN** (sejak 2026-09-04) — bukan cuma current-state. Mengedit assignment tahun BUKAN aktif (mis. isi wali kelas tahun depan dari jauh hari) TIDAK BOLEH mengubah `guru.jabatan` hari ini.

---

## 1. Skema Tabel Akhir (final state, urut kolom sesuai migrasi kronologis)

### 1.1 `guru`
Migrasi pembentuk (kronologis): `2026-08-01-000001_CreateGuruTable` → `2026-08-04-000003_AddTeacherImportFieldsAndSimplifyClasses` → `2026-08-20-000003_AddNomorUrutToGuru` → `2026-08-27-000001_CreateWaliKelasTable` (drop `kelas_id`, drop FK) → `2026-09-04-000003_AddTahunAjaranMulaiToGuru` → `2026-09-07-000002_DropGuruNoHandphoneUniqueIndex` (drop unique index) → `2026-09-07-000003_AddStatusKeluarToGuru`.

| Kolom | Tipe | Null | Default | Keterangan |
|---|---|---|---|---|
| guru_id | INT(11) UNSIGNED, AUTO_INCREMENT, PK | NOT NULL | — | |
| nama | VARCHAR(100) | NOT NULL | — | |
| nip | VARCHAR(30) | NULL | — | Nomor Induk Pegawai opsional. Indexed (non-unique, `addKey('nip')`) |
| nomor_urut | INT(11) | NULL | — | UNIQUE (`uniq_guru_nomor_urut`); NULL boleh berulang. Bagian kode jadwal "35K" |
| jenis_kelamin | ENUM('L','P') | NULL (diubah dari NOT NULL di migrasi 2026-08-04) | — | |
| jabatan | ENUM('guru_kelas','guru_bidang','karyawan') | NOT NULL | — | Selaras kategori sistem presensi |
| mata_pelajaran | VARCHAR(100) | NULL | — | Catatan bebas saja, BUKAN penugasan resmi |
| jumlah_jam_mengajar | SMALLINT UNSIGNED | NULL | — | (kolom lama, tidak lagi diisi dari form — beban mengajar sungguhan dihitung dari Jadwal Pelajaran) |
| gelar_terakhir | VARCHAR(100) | NULL | — | Dipakai perhitungan bonus gaji |
| no_handphone | VARCHAR(20) | NULL | — | ~~UNIQUE~~ (unique index `uniq_guru_no_handphone` DIHAPUS 2026-09-07 — lihat catatan bug di bawah). Keunikan sekarang MURNI level aplikasi via rule `no_handphone_unik_aktif` |
| alamat | VARCHAR(255) | NULL | — | |
| status_aktif | TINYINT(1) UNSIGNED | NOT NULL | 1 | 0 = arsip |
| status_keluar | ENUM('purna_bakti','resign','diberhentikan') | NULL | NULL | Diisi TU saat mengarsipkan (opsional). NULL = masih aktif ATAU diarsipkan sebelum kolom ini ada |
| tahun_ajaran_mulai_id | INT(11) UNSIGNED, FK→tahun_ajaran (SET NULL) | NULL | NULL | Tahun pertama guru terdaftar. NULL = staf lama, tidak dibatasi. **Field ini disebut di komentar migrasi akan "menyusul di form create/edit" TAPI berdasar pembacaan Guru.php store()/update() TIDAK PERNAH benar-benar diisi dari form manapun** — kolom ada di DB tapi belum dipakai di controller/view manapun yang dibaca. |
| kelompok, kelas_id | — | — | — | **DIHAPUS** 2026-08-27 (kolom lama `kelas_id` dibuang setelah data dipindah ke tabel `wali_kelas`) |
| created_at, updated_at | DATETIME | NULL | — | |

Catatan validasi HP: `2026-08-04-000003` sempat menambah UNIQUE `uniq_guru_no_handphone`; `2026-09-07-000002` menghapusnya lagi karena ternyata mengunci nomor HP guru yang sudah diarsipkan selamanya (constraint DB tidak tahu soal `status_aktif`). Sekarang keunikan HP HANYA dijaga di level aplikasi.

### 1.2 `kelas`
Migrasi: `2026-05-03-000003_CreateKelasTable` → `2026-07-08-000001_RenamePrimaryKeys` (`id`→`kelas_id`) → `2026-07-20-000002_AddWaliKelasToKelas` (tambah kolom teks `wali_kelas`) → `2026-08-04-000001_AddArchiveLifecycle` (tambah `is_active`, `archived_at`) → `2026-08-04-000002_SupportTkClassLevels` (`tingkat` TINYINT→VARCHAR(20)) → `2026-08-04-000003` (`kelompok` jadi nullable, tambah UNIQUE `(tingkat,nama_kelas)`) → `2026-08-27-000001_CreateWaliKelasTable` (**drop kolom `wali_kelas`**, data dipindah ke tabel relasi).

| Kolom | Tipe | Null | Default | Keterangan |
|---|---|---|---|---|
| kelas_id | INT(11) UNSIGNED, AUTO_INCREMENT, PK | NOT NULL | — | |
| nama_kelas | VARCHAR(100) | NOT NULL | — | Contoh "Kelas 1 Al-Khowarizmi". Auto-prefix tingkat, lihat `formatNamaKelas()` |
| tingkat | VARCHAR(20) | NOT NULL | — | Semula TINYINT(1-6), diubah VARCHAR utk dukung "TK-A"/"TK-B"/"Playgroup". FK-logis ke `tingkat.kode` (bukan FK constraint DB, tapi divalidasi `is_not_unique[tingkat.kode]`) |
| kelompok | TINYINT(1) UNSIGNED | NULL | NULL | Rombel A-D — sudah TIDAK dipakai input pengguna |
| is_active | TINYINT(1) UNSIGNED | NOT NULL | 1 | 0 = arsip |
| archived_at | DATETIME | NULL | — | |
| created_at, updated_at | DATETIME | NULL | — | |

UNIQUE key: `uniq_kelas_tingkat_nama (tingkat, nama_kelas)` — dicek juga manual di controller.

### 1.3 `tahun_ajaran`
Migrasi: `2026-05-14-000001_CreateTahunAjaranTable` → `2026-07-08-000001_RenamePrimaryKeys` (`id`→`tahun_ajaran_id`).

| Kolom | Tipe | Null | Default | Keterangan |
|---|---|---|---|---|
| tahun_ajaran_id | INT(11) UNSIGNED, AUTO_INCREMENT, PK | NOT NULL | — | |
| nama | VARCHAR(20) | NOT NULL | — | Format "2024/2025". UNIQUE |
| is_active | TINYINT(1) UNSIGNED | NOT NULL | 0 | Hanya 1 baris `is_active=1` di seluruh tabel (dijaga aplikasi) |
| created_at, updated_at | DATETIME | NULL | — | |

### 1.4 `wali_kelas`
Migrasi: `2026-08-27-000001_CreateWaliKelasTable` → `2026-09-04-000004_AddTahunAjaranScopingToWaliKelas`.

| Kolom | Tipe | Null | Default | Keterangan |
|---|---|---|---|---|
| wali_kelas_id | INT(11) UNSIGNED, AUTO_INCREMENT, PK | NOT NULL | — | |
| kelas_id | INT(11) UNSIGNED, FK→kelas (CASCADE) | NOT NULL | — | |
| guru_id | INT(11) UNSIGNED, FK→guru (CASCADE) | NOT NULL | — | |
| tahun_ajaran_id | INT(11) UNSIGNED, FK→tahun_ajaran (CASCADE), constraint `fk_wali_kelas_tahun_ajaran` | NOT NULL | — | Ditambah belakangan sebagai NULLABLE lalu di-backfill lalu NOT NULL |
| urutan | TINYINT(3) UNSIGNED | NOT NULL | 1 | "wali ke berapa" — TIDAK dibatasi ke maks 2 lagi sejak 2026-08-27 (histori: 1:1 ketat → maks 2 → bebas) |
| created_at, updated_at | DATETIME | NULL | — | |

UNIQUE keys FINAL: `(guru_id, tahun_ajaran_id)` — 1 guru cuma 1 baris per tahun (wali 1 kelas per tahun); `(kelas_id, tahun_ajaran_id, urutan)` — 1 slot per kelas per tahun cuma 1 guru. (Key lama `guru_id` sendirian dan `kelas_id,urutan` sendirian sudah di-drop.)

### 1.5 `kepala_sekolah`
Migrasi: `2026-09-04-000002_CreateKepalaSekolahTable` → `2026-09-04-000005_AddTahunAjaranScopingToKepalaSekolah`.

| Kolom | Tipe | Null | Default | Keterangan |
|---|---|---|---|---|
| kepala_sekolah_id | INT(11) UNSIGNED, AUTO_INCREMENT, PK | NOT NULL | — | |
| guru_id | INT(11) UNSIGNED, FK→guru (CASCADE) | NOT NULL | — | |
| tahun_ajaran_id | INT(11) UNSIGNED, FK→tahun_ajaran (CASCADE), constraint `fk_kepala_sekolah_tahun_ajaran` | NOT NULL | — | |
| created_at, updated_at | DATETIME | NULL | — | |

UNIQUE key final: `(guru_id, tahun_ajaran_id)`. Bukan jabatan/role terpisah — hanya penanda tambahan di atas guru/pegawai yang sudah ada, kandidatnya SEMUA guru aktif (`guru_kelas`/`guru_bidang`/`karyawan`), maks 1 baris per tahun ajaran per instalasi (1 instalasi = 1 unit, tidak ada kolom unit_id).

### 1.6 `ekskul`
Migrasi: `2026-08-30-000001_CreateEkskulTables`.

| Kolom | Tipe | Null | Default | Keterangan |
|---|---|---|---|---|
| ekskul_id | INT(11) UNSIGNED, AUTO_INCREMENT, PK | NOT NULL | — | |
| nama | VARCHAR(100) | NOT NULL | — | |
| pembina | VARCHAR(100) | NULL | — | Teks bebas, BUKAN FK ke guru (bisa pelatih eksternal) |
| hari | VARCHAR(20) | NULL | — | Teks bebas mis. "Sabtu" |
| deskripsi | TEXT | NULL | — | |
| is_active | TINYINT(1) | NOT NULL | 1 | 0 = arsip |
| archived_at | DATETIME | NULL | — | |
| created_at, updated_at | DATETIME | NULL | — | |

### 1.7 `ekskul_siswa`
| Kolom | Tipe | Null | Default | Keterangan |
|---|---|---|---|---|
| id | INT(11) UNSIGNED, AUTO_INCREMENT, PK | NOT NULL | — | |
| ekskul_id | INT(11) UNSIGNED, FK→ekskul (CASCADE) | NOT NULL | — | |
| siswa_id | INT(11) UNSIGNED, FK→siswa (CASCADE) | NOT NULL | — | |
| tanggal_daftar | DATE | NOT NULL | — | |
| created_at | DATETIME | NULL | — | (tidak ada `updated_at` — `useTimestamps=false`, hanya `created_at` diisi manual) |

UNIQUE key: `(ekskul_id, siswa_id)` — 1 siswa boleh ikut >1 ekskul (tidak eksklusif seperti kelas), cuma tidak boleh dobel di ekskul yang sama.

### 1.8 `tingkat` (master jenjang)
Migrasi: `2026-08-20-000001_CreateTingkatTable`.

| Kolom | Tipe | Null | Default | Keterangan |
|---|---|---|---|---|
| tingkat_id | INT(11) UNSIGNED, AUTO_INCREMENT, PK | NOT NULL | — | |
| kode | VARCHAR(20), UNIQUE | NOT NULL | — | Identitas tetap, dipakai `kelas.tingkat` & `kurikulum_alokasi` |
| nama | VARCHAR(50) | NOT NULL | — | Label tampilan, bebas diubah |
| urutan | INT(11) | NOT NULL | 0 | |
| is_active | TINYINT(1) | NOT NULL | 1 | |
| created_at, updated_at | DATETIME | NULL | — | |

Seed awal: `Playgroup`(urutan 1), `TK-A`(2), `TK-B`(3), lalu kode "1".."6" = "Kelas 1 SD".."Kelas 6 SD" (urutan 11-16). Nilai `kelas.tingkat` lama yang tak dikenal ikut didaftarkan otomatis saat migrasi (urutan 99).

### 1.9 `guru_mata_pelajaran` (relasi guru↔mapel, dipakai Guru Pengampu)
Migrasi: `2026-08-19-000003_CreateGuruMataPelajaranTable` → `..-000004_AddTingkatToGuruMataPelajaran` → `..-000005_DropOldGuruMataPelajaranUniqueKey`.

| Kolom | Tipe | Null | Default | Keterangan |
|---|---|---|---|---|
| guru_mata_pelajaran_id | INT(11) UNSIGNED, AUTO_INCREMENT, PK | NOT NULL | — | |
| guru_id | INT(11) UNSIGNED, FK→guru (CASCADE) | NOT NULL | — | |
| mata_pelajaran_id | INT(11) UNSIGNED, FK→mata_pelajaran (CASCADE) | NOT NULL | — | |
| tingkat | VARCHAR(20) | NULL | — | NULL = berlaku UNIVERSAL semua tingkat; kalau diisi HARUS sama persis dgn `kelas.tingkat`/`tingkat.kode` |
| created_at | DATETIME | NULL | — | (tidak ada updated_at) |

UNIQUE key final: `uniq_guru_mapel_tingkat (guru_id, mata_pelajaran_id, tingkat)`. Key lama 2-kolom `(guru_id, mata_pelajaran_id)` **sudah di-drop** — histori bug: key lama itu MASIH aktif membatasi 1 guru cuma 1 baris per mapel walau tingkat beda, menyebabkan "Duplicate entry" palsu untuk kasus sah (guru ampu mapel sama di 2 tingkat berbeda).

---

## 2. Daftar Route

| Route | Method | Controller::method | Filter |
|---|---|---|---|
| `guru` | GET | `Guru::index` | login |
| `guru/create` | GET | `Guru::create` | login |
| `guru/store` | POST | `Guru::store` | login |
| `guru/print` | GET | `Guru::print` | login |
| `guru/arsip` | GET | `Guru::arsip` | login |
| `guru/import` | GET | `Guru::import` | login |
| `guru/process-import` | POST | `Guru::previewImport` | login |
| `guru/preview-import` | GET | `Guru::showPreviewImport` | login |
| `guru/apply-import` | POST | `Guru::applyImport` | login |
| `guru/download-template` | GET | `Guru::downloadTemplate` | login |
| `guru/(:num)` | GET | `Guru::detail/$1` | login |
| `guru/(:num)/update` | POST | `Guru::update/$1` | login |
| `guru/(:num)/delete` | POST | `Guru::delete/$1` | login |
| `guru/bulk-delete` | POST | `Guru::bulkDelete` | login |
| `kelas` | GET | `Kelas::index` | login, pendidikan |
| `kelas/store` | POST | `Kelas::store` | login, pendidikan |
| `kelas/(:num)` | GET | `Kelas::detail/$1` | login, pendidikan |
| `kelas/(:num)/edit` | GET | `Kelas::edit/$1` | login, pendidikan |
| `kelas/(:num)/print` | GET | `Kelas::printKelas/$1` | login, pendidikan |
| `kelas/print-tingkat/(:segment)` | GET | `Kelas::printTingkat/$1` | login, pendidikan |
| `kelas/(:num)/update` | POST | `Kelas::update/$1` | login, pendidikan |
| `kelas/(:num)/archive` | POST | `Kelas::archive/$1` | login, pendidikan |
| `kelas/(:num)/restore` | POST | `Kelas::restore/$1` | login, pendidikan |
| `kelas/(:num)/add-siswa` | POST | `Kelas::addSiswa/$1` | login, pendidikan |
| `kelas/(:num)/remove/(:num)` | POST | `Kelas::removeSiswa/$1/$2` | login, pendidikan |
| `ekskul` | GET | `Ekskul::index` | login, pendidikan |
| `ekskul/store` | POST | `Ekskul::store` | login, pendidikan |
| `ekskul/(:num)` | GET | `Ekskul::detail/$1` | login, pendidikan |
| `ekskul/(:num)/edit` | GET | `Ekskul::edit/$1` | login, pendidikan |
| `ekskul/(:num)/update` | POST | `Ekskul::update/$1` | login, pendidikan |
| `ekskul/(:num)/archive` | POST | `Ekskul::archive/$1` | login, pendidikan |
| `ekskul/(:num)/restore` | POST | `Ekskul::restore/$1` | login, pendidikan |
| `ekskul/(:num)/add-siswa` | POST | `Ekskul::addSiswa/$1` | login, pendidikan |
| `ekskul/(:num)/remove/(:num)` | POST | `Ekskul::removeSiswa/$1/$2` | login, pendidikan |
| `tahun-ajaran` | GET | `TahunAjaran::index` | login, pendidikan |
| `tahun-ajaran/store` | POST | `TahunAjaran::store` | login, pendidikan |
| `tahun-ajaran/(:num)/set-active` | POST | `TahunAjaran::setActive/$1` | login, pendidikan |
| `tahun-ajaran/(:num)/delete` | POST | `TahunAjaran::delete/$1` | login, pendidikan |
| `penugasan-mengajar` | GET | `PenugasanMengajar::index` | login, pendidikan |
| `penugasan-mengajar/cari-guru-kelas` | GET | `PenugasanMengajar::cariGuruKelas` | login, pendidikan |
| `penugasan-mengajar/wali-kelas` | POST | `PenugasanMengajar::assignWaliKelas` | login, pendidikan |
| `penugasan-mengajar/guru/(:num)/update` | POST | `PenugasanMengajar::updateBaris/$1` | login, pendidikan |
| `penugasan-mengajar/guru-mapel/(:num)/delete` | POST | `PenugasanMengajar::removeMapelGuru/$1` | login, pendidikan |
| `penugasan-mengajar/import-wali-kelas` | GET | `PenugasanMengajar::importWaliKelas` | login, pendidikan |
| `penugasan-mengajar/download-template-wali-kelas` | GET | `PenugasanMengajar::downloadTemplateWaliKelas` | login, pendidikan |
| `penugasan-mengajar/process-import-wali-kelas` | POST | `PenugasanMengajar::processImportWaliKelas` | login, pendidikan |
| `penugasan-mengajar/import-guru-mapel` | GET | `PenugasanMengajar::importGuruMapel` | login, pendidikan |
| `penugasan-mengajar/download-template-guru-mapel` | GET | `PenugasanMengajar::downloadTemplateGuruMapel` | login, pendidikan |
| `penugasan-mengajar/process-import-guru-mapel` | POST | `PenugasanMengajar::processImportGuruMapel` | login, pendidikan |
| `kepala-sekolah` | GET | `KepalaSekolah::index` | login, pendidikan |
| `kepala-sekolah/cari-kandidat` | GET | `KepalaSekolah::cariKandidat` | login, pendidikan |
| `kepala-sekolah/tetapkan` | POST | `KepalaSekolah::tetapkan` | login, pendidikan |
| `kepala-sekolah/kosongkan` | POST | `KepalaSekolah::kosongkan` | login, pendidikan |

Filter `pendidikan` = `PendidikanFilter`: redirect `/dashboard` + flash error `'Modul ini tidak tersedia untuk tipe instalasi saat ini (Perusahaan).'` kalau `is_pendidikan_module_active()` false. Route `guru/*` TIDAK memakai filter `pendidikan` (guru selalu ada, hanya dibatasi jabatan di dalam controller).

---

## 3. GURU / PEGAWAI — `app/Controllers/Guru.php`

### 3.1 Helper privat
- `allowedJabatan(): ?array` — `['karyawan']` kalau `effective_install_type()==='perusahaan'`, else `null` (tak dibatasi).
- `isPerusahaan(): bool` — alias `effective_install_type()==='perusahaan'`.
- `findInScope(int $id): array` — `find($id)`; 404 (pesan `"Guru dengan ID $id tidak ditemukan."`) kalau tidak ada ATAU jabatannya di luar `allowedJabatan()`.

### 3.2 `index()` — GET `guru`
Pola IDENTIK `Siswa::index()`.
1. `jabatanFilter=allowedJabatan()`; `keyword=trim(getGet('q'))`.
2. `perPage`: harus `[50,100,150,200]` else default 50.
3. `page=max(1,(int)getGet('page'))`.
4. **keyword!==''**: ambil SEMUA guru aktif scope via `getAktifWithKelas($jabatanFilter)` (tanpa limit), filter `BoyerMoore::search($semua,$keyword,['nama','nip','no_handphone'])`. Hasil sekaligus tanpa paginasi. Total per jabatan dari HASIL PENCARIAN.
5. **keyword kosong**: kartu ringkasan SELALU dari SELURUH data aktif via `ringkasanJabatan()`. `totalPages=ceil(totalGuru/perPage)`; data diambil `getAktifWithKelas($jabatanFilter,perPage,offset)`.
6. `title`: 'Data Pegawai'(perusahaan)/'Data Guru & Pegawai'(pendidikan).
7. **AJAX fragment**: `getGet('ajax')==='1'` (STRING LITERAL, bukan deteksi header — sengaja "jelas & mudah diuji lewat curl") → return HANYA `guru/_hasil`, else `guru/index`.

### 3.3 `create()` — GET `guru/create`
`view('guru/create')` dengan `title`+`isPerusahaan`.

### 3.4 `guruRules(?int $excludeId=null)` — validasi PERSIS, dipakai store() & update()
- **nip**: `permit_empty|max_length[30]|numeric` + (`is_unique[guru.nip,guru_id,{excludeId}]` update / `is_unique[guru.nip]` create). `numeric`→"NIP hanya boleh berisi angka."
- **no_handphone**: `required|max_length[20]|numeric` + rule custom `no_handphone_unik_aktif[{excludeId atau 0}]` — SELALU dipanggil berkurung (termasuk create dengan literal "0" = tidak kecualikan siapa pun). Pesan: required→"No. Handphone wajib diisi."; numeric→"No. Handphone hanya boleh berisi angka."; no_handphone_unik_aktif→"No. Handphone sudah digunakan guru/pegawai lain yang masih aktif."
- **nama**: `required|min_length[3]|max_length[100]|regex_match[REGEX_NAMA]`. Pesan regex_match: "Nama hanya boleh berisi huruf, spasi, titik, koma, strip, dan tanda petik satu."
- **jenis_kelamin**: `permit_empty|in_list[L,P]`.
- **jabatan**: `required|in_list[{allowedJabatanForm}]` dimana `allowedJabatanForm = isPerusahaan() ? ['karyawan'] : ['guru','karyawan']`. **PENTING**: nilai form 'guru'/'karyawan' BUKAN nilai DB asli (tetap 3: guru_kelas/guru_bidang/karyawan) — lihat mapping store()/update().
- Tidak ada rule `kelas_id` SENGAJA — wali kelas HANYA lewat menu Kelola Kelas.
- **mata_pelajaran**: `permit_empty|max_length[100]` (catatan bebas).
- **gelar_terakhir**: `permit_empty|max_length[100]`.
- **alamat**: `permit_empty|max_length[255]`.

### 3.5 `store()` — POST `guru/store`
1. Validasi `guruRules()`; gagal→re-render `guru/create` dengan validation.
2. `jabatanForm = isPerusahaan() ? 'karyawan' : getPost('jabatan')`.
3. `jabatan = ($jabatanForm==='karyawan') ? 'karyawan' : 'guru_bidang'` — guru baru SELALU mulai `guru_bidang`.
4. `insert(['nama','nip'=>getPost('nip')?:null,'jenis_kelamin'=>getPost('jenis_kelamin')?:null,'jabatan','mata_pelajaran'=>trim(getPost('mata_pelajaran'))?:null,'gelar_terakhir'=>trim(getPost('gelar_terakhir'))?:null,'no_handphone'=>preg_replace('/\D+/','',getPost('no_handphone')),'alamat'=>getPost('alamat')])`. `kelas_id` sengaja tidak diisi.
5. Redirect `guru` message "{label} berhasil ditambahkan." (label='Data pegawai'/'Data guru').

### 3.6 `detail(int $id)` — GET `guru/(:num)`
1. `guru=findInScope($id)`.
2. `waliDari=waliKelasModel->getKelasByGuru($id)` (tahun AKTIF default); `guru['nama_kelas']=waliDari['nama_kelas']??null`.
3. `bebanMengajar=hitungBebanMengajar($guru)`: null kalau jabatan='karyawan' ATAU tidak ada TA aktif; else `getBebanGuruPerSemester(guru_id,taAktif_id)`→`{ganjil,genap,total}` DIHITUNG dari jadwal_pelajaran.
4. `tahunAktif=getActive()`.
5. Render `guru/detail`.

### 3.7 `update(int $id)` — POST `guru/(:num)/update`
1. `guru=findInScope($id)`; `waliDariSaatIni=getKelasByGuru($id)`; `guru['nama_kelas']`.
2. Validasi `guruRules($id)`; gagal→re-render `guru/detail` + validation.
3. `jabatanForm=isPerusahaan()?'karyawan':getPost('jabatan')`. 'karyawan'→jabatan='karyawan'. Else ('guru' dipilih)→**PERTAHANKAN** guru_kelas/guru_bidang yang SUDAH ada (`jabatan=$guru['jabatan']==='karyawan'?'guru_bidang':$guru['jabatan']`) — nilai lama tidak pernah diketik ulang, derivasi otomatis dari Kelola Kelas.
4. **Guard krusial** (histori bug "Tiara Puspita" 2026-08-27): `$waliDariSaatIni!==null` (SEDANG jadi wali kelas nyata) DAN jabatan baru TIDAK termasuk `['guru_kelas','guru_bidang']`→**TOLAK**, redirect back `withInput()`+error PERSIS: `"{nama} masih tercatat sbg wali kelas {nama_kelas}. Tetapkan wali kelas pengganti dulu di menu Kelola Kelas sebelum mengubah jabatannya."` Guard SENGAJA jabatan-agnostic (cek `$waliDariSaatIni`, bukan `jabatan==='guru_kelas'` — bug lama HANYA cek jabatan lama=guru_kelas, padahal Guru Bidang JUGA bisa jadi wali kelas, jadi guru_bidang yang sungguhan wali bisa lolos diubah jadi Karyawan tanpa guard menyala).
5. `update($id,[...,'status_aktif'=>getPost('status_aktif')!==null?1:0])` — checkbox HTML: tidak dicentang=tidak terkirim sama sekali→null→0. Keanggotaan wali kelas TIDAK disentuh di sini.
6. Redirect `guru` message "{label} berhasil diperbarui."

### 3.8 `delete(int $id)` — POST `guru/(:num)/delete` (arsipkan, BUKAN hapus fisik)
1. `guru=findInScope($id)`.
2. `statusKeluar=getPost('status_keluar')`; `data=['status_aktif'=>0]`; kalau `statusKeluar` termasuk `STATUS_KELUAR_VALID=['purna_bakti','resign','diberhentikan']`→ikutkan `data['status_keluar']`.
3. `update($id,$data)`.
4. Redirect `guru` message "{label} {nama} berhasil dinonaktifkan."

### 3.9 `bulkDelete()` — POST `guru/bulk-delete`
1. `ids=getPost('ids')`; kosong/bukan array→error "Tidak ada data yang dipilih."
2. Query `whereIn('guru_id',$ids)`, dibatasi `whereIn('jabatan',allowedJabatan())` kalau perusahaan.
3. `jumlah=countAllResults(false)` (tanpa reset builder).
4. Set `['status_aktif'=>0]` (+status_keluar sama seperti delete()), `update()` massal.
5. Redirect `guru` message "{jumlah} {label} berhasil dinonaktifkan."

### 3.10 `arsip()` — GET `guru/arsip`
`getArsipWithKelas(allowedJabatan())`. Render `guru/arsip`. Read-only + link "Detail" per baris (jalur pemulihan via centang "Aktif" di form update).

### 3.11 `print()` — GET `guru/print`
`getAktifWithKelas(allowedJabatan())` (TANPA limit). Render `guru/print` (halaman cetak polos).

### 3.12 Import (3 langkah: preview→session→apply)
**`import()`** GET: `session()->remove('preview_import_guru')`, render `guru/import`.

**`previewImport()`** POST `guru/process-import` (Langkah 1, TIDAK sentuh DB):
1. Validasi upload: file ada&valid; ekstensi xlsx/xls; maks 5MB. Pesan: "Upload gagal: {errorString}", "Format file harus .xlsx atau .xls.", "Ukuran file terlalu besar. Maksimal 5 MB."
2. Simpan `WRITEPATH.'uploads/'`, parse `Shuchkin\SimpleXLSX`. Gagal→"Gagal membaca file Excel: {parseError}"
3. Header WAJIB PERSIS: `['Nama','Jenis Kelamin','Jabatan','Gelar Terakhir','No HP']`. Tidak cocok→"Urutan kolom tidak sesuai template terbaru. Download ulang template guru sebelum mengimpor."
4. Per baris (mulai Excel ke-2), skip kalau Nama & No HP kosong.
   - `nama` wajib, else "Baris {n}: Nama wajib diisi."
   - Panjang nama/gelar>100 ATAU phone(setelah strip)>20→"Baris {n}: Nama/gelar maksimal 100 karakter dan No HP maksimal 20 angka."
   - `jenis_kelamin` via `normalizeJenisKelaminImport()`: kosong→null; l/laki-laki/laki laki/pria→'L'; p/perempuan/wanita→'P'; case-insensitive; lain(non-kosong)→"Baris {n}: Jenis Kelamin cuma boleh 'L'/'Laki-laki' atau 'P'/'Perempuan' (boleh dikosongkan)."
   - `jabatan`: perusahaan→SELALU 'karyawan' (kolom diabaikan). Else `normalizeJabatanImport()`: kosong→'guru_bidang'(default); 'guru'/'guru kelas'/'wali kelas'/'guru bidang'/'guru bidang studi'→'guru_bidang' (SEMUA sinonim SAMA — status Guru Kelas TIDAK BISA diimpor); 'karyawan'/'pegawai'→'karyawan'; lain→null→"Baris {n}: Jabatan cuma boleh 'Guru' atau 'Karyawan' (boleh dikosongkan, default Guru)."
   - `no_handphone` (strip non-digit) wajib, else "Baris {n}: No HP wajib diisi."
   - **Klasifikasi INSERT vs UPDATE**: cari guru `no_handphone=$phone AND status_aktif=1` (HANYA aktif — nomor arsip bebas dipakai ulang).
     - **ADA (UPDATE)**: sel kosong=jangan diubah. `nama` selalu ikut (untuk diff). `jenis_kelamin` ikut kalau `jkRaw!==''`. `jabatan` ikut kalau isPerusahaan(selalu dipaksa) ATAU `jabatanRaw!==''`. `gelar_terakhir` ikut kalau `!==''`. Diff dicatat ke `diff[]`. `status_aktif`/`kelas_id` TIDAK PERNAH disentuh.
     - **TIDAK ADA (INSERT)**: cek duplikat No HP DALAM file (`seenPhones`)→"Baris {n}: No HP {phone} dipakai baris lain yang sama di file ini." Data baru: nama,nip=null,jenis_kelamin,jabatan,mata_pelajaran=null,gelar_terakhir=gelar?:null,no_handphone=phone,status_aktif=1.
5. `$preview` kosong→flash `import_errors`(kalau ada)+redirect `guru/import` error "Tidak ada baris valid untuk diimport."
6. Simpan session `preview_import_guru` `{rows,ringkasan{insert,update,error},errors}`. Redirect `guru/preview-import`.
7. `finally`: file temp SELALU dihapus.

**`showPreviewImport()`** GET: session kosong→redirect error "Belum ada data untuk di-preview. Silakan upload ulang." Else render `guru/preview_import` dengan `preview`,`fieldLabels=IMPORT_FIELD_LABELS=['nama'=>'Nama','jenis_kelamin'=>'Jenis Kelamin','jabatan'=>'Jabatan','gelar_terakhir'=>'Gelar Terakhir']`.

**`applyImport()`** POST (Langkah 3):
1. Session kosong→redirect error "Belum ada data untuk diterapkan. Silakan upload ulang."
2. `transStart()`: loop rows, insert(type='insert')/update(guru_id,data)(type='update'). Hitung inserted/updated.
3. `transComplete()`; hapus session.
4. Gagal→error "Import gagal diterapkan, tidak ada data yang berubah."
5. Sukses→"Berhasil import {inserted} guru baru." + (updated>0)" {updated} guru lama diperbarui." + (inserted>0)" Lanjutkan ke menu \"Kelola Kelas\" utk menetapkan wali kelas, dan \"Guru Pengampu\" utk mata pelajaran resminya." Errors tersisa→flash `import_errors`. Redirect `guru`.

**`downloadTemplate()`**: XLSX 2 sheet. Sheet1 header `['Nama','Jenis Kelamin','Jabatan','Gelar Terakhir','No HP']`+2 baris contoh (Rudi/L/Guru/S1/081234560001, Siti Aisyah/P/Karyawan//081234560002). Sheet2 "Panduan":
```
CATATAN

Template ini cuma untuk data identitas dasar guru/pegawai.

Kolom "Jenis Kelamin" boleh dikosongkan - isi "L"/"Laki-laki" atau
"P"/"Perempuan" (tidak peduli besar-kecil huruf).

Kolom "Jabatan" boleh dikosongkan (default: Guru) - isi cuma "Guru"
atau "Karyawan". Status Guru Kelas / Guru Bidang TIDAK diisi manual
di sini lagi: guru yang ditunjuk jadi penanggung jawab kelas di menu
"Kelola Kelas" otomatis berstatus Guru Kelas, yang lain otomatis
Guru Bidang.

Kolom "No HP" WAJIB diisi. Kolom "Gelar Terakhir" OPSIONAL - kalau
diisi, langsung masuk ke field Gelar Terakhir di detail guru
(dipakai utk perhitungan bonus gaji); kalau dikosongkan di Excel,
tetap kosong di sistem (mis. utk Karyawan yang tidak relevan gelar).

Wali kelas diatur SETELAH import lewat menu "Kelola Kelas", mata
pelajaran resmi lewat "Guru Pengampu", dan jadwal mengajar lewat
"Jadwal Pelajaran" - di sana ada penjagaan supaya 1 kelas tidak
dobel wali dan 1 guru tidak bentrok jam mengajar, yang tidak bisa
dijamin lewat Excel.
```
File `template_import_guru.xlsx`.

### 3.13 `GuruValidationRules::no_handphone_unik_aktif` (`app/Validation/GuruValidationRules.php`)
Signature CI4 custom rule: `(string $str, ?string $params, array $data, ?string &$error=null): bool`. `$params`=excludeId string; "0"/null→tidak kecualikan. Query: `guru WHERE no_handphone=$str AND status_aktif=1` (+`guru_id!=excludeId` kalau ada). `countAllResults()>0`→`$error='No. Handphone sudah digunakan guru/pegawai lain yang masih aktif.'`, false. Registrasi: `Config\Validation::$ruleSets`.

---

## 4. GuruModel (`app/Models/GuruModel.php`)

`table='guru'`, `primaryKey='guru_id'`, `returnType='array'`, `useTimestamps=true`, `skipValidation=true` (validasi di controller, supaya `$validation` tersedia di view).

`allowedFields`: `nama, nip, nomor_urut, jenis_kelamin, jabatan, mata_pelajaran, jumlah_jam_mengajar, gelar_terakhir, no_handphone, alamat, status_aktif, status_keluar`. (**`tahun_ajaran_mulai_id` TIDAK ada di allowedFields** — konsisten temuan tidak pernah ditulis dari form manapun.)

Method custom:
- `getAktifWithKelas(?jabatanFilter,?limit,offset=0)`: LEFT JOIN `wali_kelas w ON w.guru_id=g.guru_id` LEFT JOIN `kelas k ON k.kelas_id=w.kelas_id`, WHERE `status_aktif=1`(+whereIn jabatan opsional), orderBy nama ASC. `nama_kelas`=kelas yang diwalikan (via relasi). Catatan: JOIN TIDAK di-scope ke tahun ajaran tertentu — otomatis ambil SEMUA baris wali_kelas guru itu; karena UNIQUE(guru_id,tahun_ajaran_id), 1 guru bisa punya wali_kelas di BEBERAPA tahun berbeda — JOIN tanpa filter tahun berpotensi menggandakan baris guru kalau wali di >1 tahun sekaligus. Perilaku APA ADANYA — pertahankan.
- `countAktif(?jabatanFilter)`: where status_aktif=1 + whereIn jabatan opsional, countAllResults().
- `ringkasanJabatan(?jabatanFilter)`: `{total,guru_kelas,guru_bidang,karyawan}` — total dari countAktif(); tiap jabatan dihitung terpisah, SELALU dari SELURUH data aktif.
- `getArsipWithKelas(?jabatanFilter)`: sama tapi status_aktif=0, tanpa limit.
- `getGuruWithMapel()`: guru `whereIn(jabatan,['guru_kelas','guru_bidang'])->where(status_aktif,1)` (karyawan dikecualikan), tiap guru dapat array `mapel`=`{guru_mata_pelajaran_id,mata_pelajaran_id,nama,tingkat}` dari JOIN guru_mata_pelajaran↔mata_pelajaran, urut nama mapel.
- `getPengajarAktif()`: sama filter, `orderBy('nomor_urut IS NULL','',false)` (bernomor duluan) lalu nomor_urut ASC lalu nama ASC.
- `getPetaNomor()`: peta `nomor_urut(int)=>baris guru` (aktif saja) — menerjemahkan kode grid "35K".
- `nomorUrutBerikutnya()`: nomor terkecil BELUM dipakai (mulai 1, loop while in_array) — tombol "beri nomor otomatis" di Penugasan Mengajar.

---

## 5. PENUGASAN MENGAJAR ("Guru Pengampu") — `app/Controllers/PenugasanMengajar.php`

**Konteks**: dulu bernama "Wali Kelas & Mata Pelajaran" tapi penetapan Wali Kelas DIPINDAH ke menu Kelola Kelas sejak 2026-08-27 — istilah "wali kelas" sengaja tidak lagi menempel di menu ini, supaya tetap relevan kalau instalasi dipakai/ditiru tipe perusahaan. Menu ini sekarang MURNI "Guru Pengampu": nomor guru (kode jadwal "35K") + tautan guru↔mata pelajaran.

### 5.1 `index()` — GET `penugasan-mengajar`
`view('penugasan_mengajar/index',['title'=>'Guru Pengampu','guruWithMapel'=>getGuruWithMapel(),'mapelDropdown'=>getDropdown(),'tingkatOptions'=>getDistinctTingkat(),'nomorBerikut'=>nomorUrutBerikutnya()])`.

### 5.2 `cariGuruKelas()` — GET, AJAX autocomplete
1. `kata=trim(getGet('q'))`; `kecuali=(int)getGet('kecuali')` (guru_id slot ITU SENDIRI, tidak disaring); `tahunAjaranId=(int)(getGet('tahun')?:activeTahunAjaranId())` — cek "sudah wali kelas lain" DI TAHUN yang SEDANG DIEDIT saja.
2. Kandidat: `guru->whereIn(jabatan,['guru_kelas','guru_bidang'])->where(status_aktif,1)->orderBy(nama)`. **BUG NYATA "Tiara Puspita" 2026-08-27**: filter dulu SALAH cuma `where('jabatan','guru_kelas')` — semua guru_bidang (termasuk yang SEDANG wali kelas nyata) tidak pernah muncul. Diperbaiki `whereIn(['guru_kelas','guru_bidang'])`.
3. `sudahJadiWali=array_map('intval',array_column(where(tahun_ajaran_id,$ta)->findAll(),'guru_id'))`. **BUG NYATA cast wajib**: MySQLi mengembalikan guru_id sebagai STRING; `in_array(...,true)` strict compare tanpa cast int TIDAK PERNAH bekerja. Ditemukan lewat log debug 2026-08-27.
4. Loop: skip kalau `id!==kecuali && in_array(id,sudahJadiWali,true)`; skip kalau `kata!=='' && stripos(nama,kata)===false`. Hasil `{guru_id,nama}`, `array_slice(...,0,20)`, JSON.

### 5.3 `assignWaliKelas()` — POST `penugasan-mengajar/wali-kelas`
**Histori**: sebelumnya dikunci ketat 1:1 (guru.kelas_id+kelas.wali_kelas teks), direstrukturisasi jadi tabel relasi (2026-08-27), sempat dibatasi maks 2 slot, akhirnya DIBUAT BEBAS jumlahnya.

**PENTING — perilaku AJAX/UX FINAL**: sejak 2026-08-27, baris wali kelas TERSIMPAN OTOMATIS tiap kali dipilih/dihapus lewat AJAX — **tombol "Simpan" & notifikasi sukses DIHAPUS** (alasan: dulu harus klik Simpan dulu baru guru yang dilepas bisa dipilih lagi di kelas lain, karena `cariGuruKelas()` membaca DB sungguhan bukan state form belum tersimpan). Endpoint dipanggil PER-BARIS tiap perubahan → respons `{success:true}` polos, halaman TIDAK reload.

Alur:
1. `kelasId=(int)getPost('kelas_id')`; `guruIdsRaw=(array)getPost('guru_id')`; `ajax=isAJAX()`; `tahunAjaranId=(int)(getPost('tahun_ajaran_id')?:activeTahunAjaranId())`.
2. Buang baris kosong dari `guruIdsRaw`→`guruIds`.
3. `kelas=find($kelasId)`; tidak ada→'Kelas tidak ditemukan.' (JSON 422 AJAX / redirect error).
4. `$kembali`=redirect `kelas?expand={tingkat}&tahun={ta}#kelas-{kelasId}`.
5. `count(guruIds)!==count(array_unique(guruIds))`→'Ada nama wali kelas yang sama dipilih lebih dari sekali - kosongkan salah satunya dulu.'
6. Per guruId: ditemukan DAN jabatan `['guru_kelas','guru_bidang']` else→'Salah satu wali kelas yang dipilih bukan guru/staf pengajar.'
7. `dilepasDari=tetapkanSemua($kelasId,$guruIds,$tahunAjaranId)`.
8. AJAX→`{success:true}` polos.
9. Non-AJAX: pesan per guru dengan "(dilepas dari kelas {namaLama})" kalau relevan. Kosong→"Wali kelas {nama_kelas} dikosongkan."; else→"Wali kelas {nama_kelas} disimpan: {bagian}." Redirect `$kembali()`.

### 5.4 `updateBaris(int $guruId)` — POST, simpan 1 baris (nomor urut + mata pelajaran baru)
**Histori UX (2 revisi, harus direplikasi PERSIS)**: Revisi 1 (redirect→AJAX 2026-09-07) — user mengeluh "kelamaan... tiap klik selesai selalu balik dari paling atas". Revisi 2 (final) — masih dikeluhkan "kedip & baris berpindah ke bawah". Akar masalah BUKAN scroll — kode lama memaksa baris KELUAR mode edit otomatis (tinggi mengecil mendadak) + kasus mapel baru masih RELOAD PENUH.

**Perilaku akhir BENAR**:
- Submit "Simpan" per baris SELALU AJAX, TIDAK PERNAH reload penuh (baik nomor urut berubah maupun mapel baru).
- Sukses: baris TETAP dalam mode edit sama (TIDAK toggle keluar otomatis), TIDAK scroll otomatis.
- Mata pelajaran baru dirender LANGSUNG ke DOM (`buatBarisMapel()` JS) dari JSON balikan (id,nama,tingkat) — bukan reload.
- Nomor urut badge (`js-nomor-tampil`) diupdate walau tersembunyi, `dataset.asli` ikut diupdate (supaya tombol Batal nanti benar).
- Error SEBAGIAN (1 dari 3 mapel bentrok): baris lain TETAP tersimpan, respons `success:true` + `errors[]` ditampilkan `.js-baris-error` — BUKAN session flash reload.
- Reload PENUH hanya: hapus tautan mapel (`removeMapelGuru`, konfirmasi SweetAlert), fallback non-AJAX.

Alur backend:
1. `guru=find($guruId)`; tidak ada→'Guru tidak ditemukan.'
2. `nomorRaw=trim(getPost('nomor_urut'))`; tidak kosong: harus `ctype_digit` dan >0, else 'Nomor guru tidak valid (harus angka lebih dari 0).' `nomorBaru=(int)nomorRaw`.
3. `nomorBaru!==null`: cek bentrok `guru WHERE nomor_urut=$n AND guru_id!=$guruId`; ada→"Nomor {n} sudah dipakai oleh {bentrok_nama} - kosongkan/ganti dulu nomor guru itu sebelum dipakai di sini."
4. `validTingkat=array_column(getDistinctTingkat(),'value')` — tingkat mapel baru HARUS salah satu nilai SUNGGUHAN dipakai instalasi ini.
5. Transaksi: `guru.nomor_urut` diupdate. Loop `mapel_baru[]={mata_pelajaran_id,tingkat}`: mapelId<=0→skip diam-diam; tingkat tidak valid→tambah error, skip baris; mapel tidak ditemukan→tambah error, skip; cek duplikat exact (guru_id,mapel_id,tingkat)→"{nama} sudah ditautkan ke {mapel_nama}"+(tingkat?" tingkat {tingkat}":' (universal)')+'.'; else insert, catat ke `mapelBaruHasil{id,nama,tingkat}`.
6. AJAX: `{success:true,errors:[...],ditambahkan:N,mapelBaru:[...],nomorUrut:nomorBaru}`.
7. Non-AJAX: flash `import_errors`; redirect message "Data {nama} disimpan." + (ditambahkan>0)" {ditambahkan} mata pelajaran baru ditautkan."

### 5.5 `removeMapelGuru(int $id)` — POST, hapus 1 baris guru_mata_pelajaran langsung
Redirect message='Tautan mata pelajaran dihapus.' (SATU-satunya aksi halaman ini yang reload penuh, disengaja karena destruktif & jarang berturut-turut, dikonfirmasi via SweetAlert JS).

### 5.6 Import Wali Kelas (Excel: Nama Kelas | Nama Wali Kelas)
- `importWaliKelas()` GET.
- `downloadTemplateWaliKelas()`: header `['Nama Kelas','Nama Wali Kelas']`+semua kelas aktif (wali kosong). File `template_import_wali_kelas.xlsx`.
- `processImportWaliKelas()`: peta `kelasByNama`(semua kelas), `guruKelasByNama`(guru aktif guru_kelas/guru_bidang). Loop: keduanya kosong→skip; kelas tak ditemukan→"Baris {n}: Kelas '{nk}' tidak ditemukan." skipped++; nama wali kosong→**lewati diam-diam** (wali kelas TIDAK diubah); guru tak ditemukan→"Baris {n}: Guru '{nw}' tidak ditemukan (nama harus persis sama & berjabatan Guru Kelas/Guru Bidang, aktif)." **Import SELALU isi SLOT 1 di TA AKTIF** (bukan tahun lain). Wali ke-2 hanya lewat UI. `tetapkan($kelasId,1,$guruId,activeTahunAjaranId())`. Redirect `kelas` message="Berhasil update {updated} wali kelas. {skipped} baris dilewati." + flash `import_errors`.

### 5.7 Import Guru & Mata Pelajaran (Excel: Nama Guru | Mata Pelajaran)
1 baris = 1 pasangan (guru boleh berulang di beberapa baris).
- `importGuruMapel()` GET.
- `downloadTemplateGuruMapel()`: header `['Nama Guru','Mata Pelajaran']`+semua guru aktif guru_kelas/guru_bidang urut nama. File `template_import_guru_mata_pelajaran.xlsx`.
- `processImportGuruMapel()`: peta `guruByNama` (aktif guru_kelas/guru_bidang). Loop: keduanya kosong→skip; salah satu kosong→"Baris {n}: Nama Guru dan Mata Pelajaran wajib diisi berdua."; guru tak ditemukan→"Baris {n}: Guru '{ng}' tidak ditemukan (nama harus persis sama, berjabatan Guru Kelas/Guru Bidang, aktif)." **Mata pelajaran belum ada di master OTOMATIS DIBUAT**. Tautan selalu **UNIVERSAL (tingkat=null)** — batasi per tingkat manual setelah import. Cek exists (guru_id,mapel_id,tingkat=null) sebelum insert (idempotent). Redirect message="Berhasil tautkan {linked} pasangan guru-mapel. {skipped} baris dilewati." + `import_errors`.

### 5.8 `readUploadedExcel()` (private helper, dipakai bersama 2 import di atas)
Validasi upload standar (file ada&valid; ekstensi xlsx/xls; ≤5MB; header row cocok persis `$expectedHeaders`) else "Urutan kolom tidak sesuai template terbaru. Download ulang template sebelum mengimpor." File temp SELALU dihapus.

---

## 6. KEPALA SEKOLAH — `app/Controllers/KepalaSekolah.php`

**Konsep inti**: Kepala Sekolah adalah penanda tambahan di atas guru/pegawai yang **SUDAH ADA** (pola sama persis Wali Kelas), **BUKAN** jabatan/role terpisah. Beda dari Wali Kelas: cuma **1 slot per instalasi PER TAHUN AJARAN** (bukan per-kelas); kandidatnya **SEMUA guru/pegawai aktif** (guru_kelas/guru_bidang/karyawan) — kepala sekolah sah datang dari kalangan pegawai juga. **VERSIONED PER TAHUN AJARAN sejak 2026-09-04** (migrasi `AddTahunAjaranScopingToKepalaSekolah`).

**Riwayat desain**: percobaan pertama memakai `jabatan` enum keempat **DI-REVERT** karena "Hub API menolaknya & role dasar guru itu hilang", bertentangan dengan rancangan disetujui user: "role dasar guru_kelas/guru/pegawai TIDAK diganti, kepala sekolah cuma flag tambahan". Sejak itu relasi kepala sekolah selalu tabel terpisah (`kepala_sekolah`) menunjuk `guru_id`, pola sama persis `wali_kelas`.

Tidak ada kolom `unit_id` — 1 instalasi WebArsipData = 1 unit, jadi "1 kepala sekolah per unit" otomatis berarti "maksimal 1 baris per instalasi PER TAHUN AJARAN" — **dijaga di level aplikasi** (`KepalaSekolahModel::tetapkan()` selalu hapus baris lama sebelum insert baru), **bukan** constraint DB murni. `UNIQUE(guru_id,tahun_ajaran_id)` di DB murni jaga-jaga, bukan mekanisme utama pembatas jumlah baris.

Routes (filter `['login','pendidikan']`): GET `kepala-sekolah`→index; GET `kepala-sekolah/cari-kandidat`→cariKandidat; POST `kepala-sekolah/tetapkan`→tetapkan; POST `kepala-sekolah/kosongkan`→kosongkan. Constructor instantiate `GuruModel`, `KepalaSekolahModel`, `TahunAjaranModel`.

### 6.1 `index()` — GET `kepala-sekolah`
```php
$tahunAjaranId = (int) (getGet('tahun') ?: $kepsekModel->activeTahunAjaranId());
$aktifId       = $kepsekModel->activeTahunAjaranId();
return view('kepala_sekolah/index', [
    'title' => 'Kepala Sekolah',
    'saatIni' => $kepsekModel->getSaatIni($tahunAjaranId),
    'tahunAjaranId' => $tahunAjaranId,
    'tahunAjaranAktif' => $aktifId,
    'tahunAjaranList' => $tahunAjaranModel->getDropdown(),
]);
```
1. GET `tahun` opsional; falsy→fallback tahun aktif (`activeTahunAjaranId()`).
2. `$aktifId` dihitung terpisah (dipakai view menandai kalau user sedang melihat tahun non-aktif).
3. `saatIni` = `KepalaSekolahModel::getSaatIni($tahunAjaranId)` → array `{guru_id,nama}` atau `null`.
4. Komentar docblock verbatim: "`?tahun=` (opsional) - tahun ajaran yang SEDANG DILIHAT/DIEDIT, default tahun aktif. Dipakai TU utk isi/lihat penetapan tahun DEPAN dari jauh hari tanpa mengubah apa pun di tahun yang sedang berlangsung (diminta user)." — pola sama Wali Kelas/Kelas.

### 6.2 `cariKandidat()` — GET, AJAX autocomplete
```php
$kata = trim((string) getGet('q'));
$semua = $guruModel->whereIn('jabatan',['guru_kelas','guru_bidang','karyawan'])
    ->where('status_aktif',1)->orderBy('nama','ASC')->findAll();
$hasil = [];
foreach ($semua as $g) {
    if ($kata !== '' && stripos($g['nama'],$kata)===false) continue;
    $hasil[] = ['guru_id'=>(int)$g['guru_id'],'nama'=>$g['nama'],'jabatan'=>$g['jabatan']];
}
return response()->setJSON(array_slice($hasil,0,20));
```
1. `q` dari GET, trim.
2. Query SEMUA guru `jabatan IN (guru_kelas,guru_bidang,karyawan)` & `status_aktif=1`, urut nama ASC — **filter kata kunci dilakukan di PHP** (`stripos`), BUKAN `LIKE` SQL. `q` kosong (fokus pertama kali)→SEMUA kandidat aktif tampil tanpa filter.
3. Hasil `{guru_id,nama,jabatan}` — `jabatan` ikut dikirim tapi TIDAK dipakai JS saat ini.
4. Dipotong maksimal **20** (`array_slice`), tanpa keterangan "...dan seterusnya".
5. **TIDAK ADA filter "kecualikan yang sudah jadi Kepala Sekolah"** — beda dari pola wali kelas. Komentar verbatim: "Cari kandidat aktif (semua jabatan - guru_kelas/guru_bidang/karyawan) utk autocomplete, dipanggil AJAX tiap ketik. Pola sama PenugasanMengajar::cariGuruKelas(), TANPA filter \"sudah jadi X\" krn kepala sekolah tidak eksklusif thd jabatan lain (guru_kelas yang sedang jadi wali kelas TETAP boleh sekaligus jadi kepala sekolah)."
6. Tidak ada guard cast-int MySQLi (perbandingan pakai `stripos` pada nama, bukan compare integer ID).
7. Response selalu array JSON polos (bukan `{data:[...]}`).

### 6.3 `tetapkan()` — POST `kepala-sekolah/tetapkan`
```php
$guruId = (int) getPost('guru_id');
$tahunAjaranId = (int) (getPost('tahun_ajaran_id') ?: $kepsekModel->activeTahunAjaranId());
$guru = $guruModel->find($guruId);
if (!$guru || (int)$guru['status_aktif'] !== 1) {
    return redirect()->back()->with('error','Guru/pegawai tidak ditemukan atau tidak aktif.');
}
$kepsekModel->tetapkan($guruId, $tahunAjaranId);
return redirect()->to(base_url('kepala-sekolah').'?tahun='.$tahunAjaranId)
    ->with('message', "{$guru['nama']} ditetapkan sebagai Kepala Sekolah.");
```
1. POST `guru_id`, `tahun_ajaran_id` (fallback tahun aktif kalau falsy).
2. `find($guruId)` — validasi MANUAL dengan `if` (**TIDAK ADA** `$this->validate()` CI4 sama sekali, tanpa pesan per-field).
3. Tidak ditemukan ATAU `status_aktif!==1`→redirect **back** (bukan ke `kepala-sekolah`) error verbatim: **"Guru/pegawai tidak ditemukan atau tidak aktif."**
4. Valid→`KepalaSekolahModel::tetapkan($guruId,$tahunAjaranId)`.
5. Redirect ke `kepala-sekolah?tahun={tahunAjaranId}` (**bukan** `redirect()->back()`) — supaya tetap di tahun yang baru diedit.
6. Message sukses verbatim: **"{nama} ditetapkan sebagai Kepala Sekolah."** (nama disisipkan langsung tanpa `esc()` di controller).
**AJAX vs non-AJAX**: controller TIDAK mendeteksi `X-Requested-With` sama sekali — response SELALU `redirect()` HTTP, bukan JSON. View memanggil via `fetch()` dengan header `X-Requested-With` TAPI **mengabaikan isi response** — JS hanya `.then(()=>window.location.reload())` tanpa cek `r.ok`/status. Flash message tetap tersimpan session dan baru tampil setelah reload. Kalau `fetch()` reject total (network error)→`#kepsekError` diisi **"Gagal menyimpan - periksa koneksi."** (murni client-side).

### 6.4 `kosongkan()` — POST `kepala-sekolah/kosongkan`
```php
$tahunAjaranId = (int) (getPost('tahun_ajaran_id') ?: $kepsekModel->activeTahunAjaranId());
$kepsekModel->kosongkan($tahunAjaranId);
return redirect()->to(base_url('kepala-sekolah').'?tahun='.$tahunAjaranId)->with('message','Kepala Sekolah dikosongkan.');
```
1. POST `tahun_ajaran_id` fallback tahun aktif.
2. `kosongkan($tahunAjaranId)` — hapus baris untuk tahun itu SAJA.
3. **Tidak ada validasi apapun** sebelumnya — idempotent, bisa dipanggil walau memang belum ada kepala sekolah di tahun itu.
4. Redirect `kepala-sekolah?tahun={id}` message verbatim: **"Kepala Sekolah dikosongkan."**
5. Dipanggil dari form biasa (native submit, BUKAN fetch) — didahului konfirmasi SweetAlert2 di JS (§6.8).

### 6.5 `KepalaSekolahModel` (`app/Models/KepalaSekolahModel.php`)
Config: `table='kepala_sekolah'`, `primaryKey='kepala_sekolah_id'`, `useTimestamps=true`, `returnType='array'`, `allowedFields=['guru_id','tahun_ajaran_id']`.

Komentar class-level verbatim: "Relasi guru <-> kepala sekolah - pola sama persis WaliKelasModel, tapi lebih sederhana (maks 1 baris PER TAHUN AJARAN, bukan N-per-kelas)... VERSIONED PER TAHUN AJARAN sejak 2026-09-04 - \"maks 1 baris\" sekarang berarti \"maks 1 baris PER TAHUN\", bukan 1 baris selamanya - tetapkan()/kosongkan() HARUS discope ke 1 tahun_ajaran_id, TIDAK BOLEH lagi wipe SELURUH tabel."

**`activeTahunAjaranId(): int`**: raw query-builder ke `tahun_ajaran WHERE is_active=1`. Tidak ada baris→**throw** `RuntimeException('Tidak ada tahun ajaran aktif - set dulu lewat menu Tahun Ajaran.')` (TIDAK di-catch di controller manapun). Pola identik `WaliKelasModel::activeTahunAjaranId()` (direplikasi terpisah, bukan lewat trait/parent).

**`getSaatIni(?int $tahunAjaranId=null): ?array`**: default tahun aktif kalau null. JOIN `kepala_sekolah`↔`guru`, ambil `{guru_id,nama}` untuk 1 tahun ajaran, `getRowArray()` (baris pertama saja). **CATATAN GAP (tidak ada komentar eksplisit di kode)**: query ini **TIDAK memfilter `guru.status_aktif=1`** — kalau kepala sekolah yang sedang menjabat dinonaktifkan lewat menu Guru (`Guru::delete()`/`deleteBulk()` hanya set `status_aktif=0`, TIDAK menyentuh tabel `kepala_sekolah` sama sekali — dikonfirmasi tidak ada referensi `kepala_sekolah`/`KepalaSekolahModel` di `Guru.php`), `getSaatIni()` TETAP mengembalikan guru itu sebagai "Kepala Sekolah saat ini" walau nonaktif — tidak ada mekanisme otomatis mengosongkan slot, harus manual klik "Kosongkan".

**`tetapkan(int $guruId, ?int $tahunAjaranId=null): void`**: default tahun aktif. Transaksi: (1) DELETE semua baris `tahun_ajaran_id=$ta` SAJA (bukan wipe seluruh tabel), (2) INSERT `{guru_id,tahun_ajaran_id}`. Tidak ada validasi guru di level model (sudah dicek controller). Komentar verbatim (histori PENTING): "SENGAJA scoped where tahun_ajaran_id (BUKAN emptyTable() lagi sejak jadi per-tahun - wipe seluruh tabel akan ikut menghapus riwayat tahun lain yang harus tetap ada)." — konfirmasi method ini DULU pakai `emptyTable()` (wipe SELURUH tabel) sebelum migrasi 2026-09-04 — perilaku lama TIDAK BOLEH direplikasi.

**`kosongkan(?int $tahunAjaranId=null): void`**: `DELETE WHERE tahun_ajaran_id=$ta` saja (scoped sama seperti tetapkan). Idempotent.

### 6.6 Migrasi `kepala_sekolah`
`2026-09-04-000002_CreateKepalaSekolahTable.php` (awal): `kepala_sekolah_id`(PK auto), `guru_id`(FK→guru CASCADE, **UNIQUE tunggal**), `created_at`,`updated_at`(nullable). BELUM ada `tahun_ajaran_id`.

Komentar verbatim histori: "Kepala Sekolah - pola SAMA PERSIS dgn wali_kelas (2026-08-27)... percobaan pertama pakai jabatan enum keempat REVERTED... Tidak ada kolom unit_id di sini - 1 instalasi WebArsipData = 1 unit... UNIQUE(guru_id) murni jaga-jaga... bukan mekanisme utama pembatas jumlah baris."

`2026-09-04-000005_AddTahunAjaranScopingToKepalaSekolah.php` (Fase 2, hari sama): tambah `tahun_ajaran_id` (nullable dulu, after guru_id) → backfill ke tahun aktif SAAT MIGRASI (kalau ada baris lama TAPI tidak ada tahun aktif→**throw** `RuntimeException('Migrasi kepala_sekolah per-tahun-ajaran gagal: ada baris kepala_sekolah lama tapi tidak ada tahun ajaran aktif. Set 1 tahun ajaran aktif dulu sebelum update ini bisa jalan.')`) → kolom jadi NOT NULL → tambah `UNIQUE KEY guru_id_tahun_ajaran_id(guru_id,tahun_ajaran_id)` **SEBELUM** drop `UNIQUE(guru_id)` lama (urutan WAJIB, index lama menopang FK) → tambah FK raw SQL `fk_kepala_sekolah_tahun_ajaran`→`tahun_ajaran` CASCADE. `down()`: hapus SEMUA baris SELAIN tahun aktif (data loss disengaja saat rollback).

**Struktur final**: `kepala_sekolah_id`(PK), `guru_id`(FK CASCADE), `tahun_ajaran_id`(NOT NULL, FK CASCADE `fk_kepala_sekolah_tahun_ajaran`), `created_at`,`updated_at`. Index: `UNIQUE(guru_id,tahun_ajaran_id)`.

### 6.7 View `kepala_sekolah/index.php`
Card `col-lg-7`. Header: "Kepala Sekolah". Teks penjelasan verbatim: "Kepala Sekolah BUKAN jabatan terpisah - orang yang ditunjuk di sini TETAP guru/guru kelas/ pegawai seperti biasa (jam mengajar, presensi, dst tidak berubah), cuma dapat kapasitas tambahan menyetujui izin guru di aplikasi Mobile-app." (konfirmasi fungsi bisnis: approval izin guru di Mobile-app terpisah, bukan fitur WebArsipData sendiri).

Dropdown TA: `onchange` langsung `window.location.href=base_url('kepala-sekolah')+'?tahun='+this.value` (navigasi penuh, BUKAN AJAX). Opsi label `esc(nama)`+" (aktif sekarang)" kalau match tahunAjaranAktif (loose `==`).

Alert info (tahun dilihat ≠ tahun aktif) verbatim: "Anda sedang melihat/mengisi tahun ajaran yang BUKAN tahun aktif sekarang - perubahan di sini TIDAK memengaruhi tahun ajaran yang sedang berjalan."

Blok "Kepala Sekolah saat ini": ADA→box bg-light, label kecil "Kepala Sekolah saat ini" + nama besar (`esc()`), form kosongkan di sampingnya. TIDAK ADA→alert secondary verbatim: **"Belum ada Kepala Sekolah ditetapkan."**

Form kosongkan (`action=kepala-sekolah/kosongkan`, class `form-kosongkan-kepsek`): csrf, hidden `tahun_ajaran_id`, tombol "Kosongkan" (outline-danger btn-sm).

Form tetapkan (`id=formTetapkanKepsek`, action `kepala-sekolah/tetapkan`): label "Ganti / Tetapkan Kepala Sekolah". csrf, hidden `guru_id`(`id=kepsekGuruId`, awal kosong), hidden `tahun_ajaran_id`. Input text `id=kepsekNama` placeholder verbatim **"Ketik nama guru/pegawai..."**, autocomplete off. Dropdown saran `id=kepsekSaran` (awal display:none). Error box `id=kepsekError` (awal display:none). **PENTING**: form ini TIDAK punya tombol submit terlihat — satu-satunya cara submit adalah JS fetch() dipicu klik item saran. Tidak ada `preventDefault` pada `keydown`/`submit` form ini — potensi edge-case: tekan Enter di input tanpa memilih dari daftar bisa memicu submit native browser dengan `guru_id` kosong.

### 6.8 JS behavior (`kepala_sekolah/index.php` inline script)
`URL_CARI` = `base_url('kepala-sekolah/cari-kandidat')` (json_encode). Fungsi `cari()` dipicu event `focus` DAN `input` pada `#kepsekNama`, debounce **120ms**. Fokus pertama (kata kosong)→tetap fetch `?q=` kosong→tampil SEMUA kandidat (maks 20).

Response kosong→pesan verbatim **"Tidak ada nama cocok."** Response ada isi→render `<button>` per kandidat, `data-nama` di-escape HANYA karakter `"` (`.replace(/"/g,'&quot;')`) — **BUKAN escaping HTML penuh**, `<`/`>`/`&` tidak di-escape (potensi HTML injection minor kalau nama guru mengandung karakter itu — biasanya tidak terjadi karena nama tervalidasi teks biasa saat input).

`blur` pada input→delay **150ms** sebelum sembunyikan kotak saran (sengaja, supaya `mousedown` pada tombol saran sempat tertangkap duluan sebelum blur). Pemilihan kandidat pakai event **`mousedown`** (bukan `click`) dengan `preventDefault()` — classic trick anti-blur-race, delegated ke container `kotak`.

Pilih kandidat: isi `input.value`+`hid.value`, sembunyikan kotak+error, langsung **fetch POST** ke `form.action` dengan header `X-Requested-With` + `body=new FormData(form)`. **Tidak cek response/status sama sekali** — `.then(()=>window.location.reload())` selalu reload apapun hasilnya (bahkan kalau server merespons error "Guru/pegawai tidak ditemukan atau tidak aktif." — pesan baru terlihat SETELAH reload via flash session, BUKAN via `#kepsekError`). `#kepsekError` HANYA terisi kalau fetch benar-benar **reject** (network failure, BUKAN HTTP 4xx/5xx karena fetch tidak reject untuk itu) — pesan verbatim **"Gagal menyimpan - periksa koneksi."**

Form kosongkan: `submit` dicegat `preventDefault()` → SweetAlert2: title **"Kosongkan Kepala Sekolah?"**, icon warning, confirmButtonText **"Ya, Kosongkan"**, cancelButtonText **"Batal"**, confirmButtonColor `#d33`. Konfirmasi→`self.submit()` (submit NATIVE, bukan fetch — full page reload oleh browser sungguhan).

### 6.9 Catatan UX unik modul ini
**PENTING — beda dari pola Wali Kelas/Guru Pengampu**: setelah `tetapkan()` sukses via AJAX, JS **SELALU** `window.location.reload()` — SATU-SATUNYA endpoint AJAX di ketiga modul (Wali Kelas, Guru Pengampu, Kepala Sekolah) yang tetap reload penuh. Tidak ada catatan histori bug UX untuk Kepala Sekolah seperti 2 modul lainnya — reload penuh di sini desain sengaja, bukan kekurangan. Porting C# boleh cukup refresh data setelah operasi sukses (tidak wajib meniru reload browser secara harfiah), tapi behavior akhirnya (data ter-refresh total setelah aksi) harus sama.

### 6.10 Referensi ke modul Guru — TIDAK ADA guard silang (temuan penting)
Grep menyeluruh `Guru.php` untuk `wali_kelas`/`WaliKelasModel`/`kepala_sekolah`/`KepalaSekolahModel`: Guru.php PUNYA guard eksplisit untuk Wali Kelas (§3.7 poin 4 di atas), tapi **TIDAK PUNYA guard setara untuk Kepala Sekolah sama sekali** — tidak ada import `KepalaSekolahModel`, tidak ada pengecekan di `update()`/`delete()`/`bulkDelete()`.

**Analisis kenapa ini KEMUNGKINAN BUKAN celah nyata**: kandidat kepala sekolah mencakup SEMUA jabatan valid (`guru_kelas`,`guru_bidang`,`karyawan`) — SEMUA nilai enum `jabatan` yang mungkin. Karena form edit guru hanya menawarkan 'guru'/'karyawan' (di-mapping ke guru_bidang/guru_kelas/karyawan), **TIDAK ADA transisi jabatan yang bisa membuat guru "tidak eligible lagi"** jadi kepala sekolah — beda dari Wali Kelas yang mensyaratkan jabatan spesifik. Jadi ketiadaan guard konsisten dengan desain "kepala sekolah bisa siapa saja pegawai/guru aktif".

**NAMUN ada gap NYATA tidak terdokumentasi eksplisit**: `Guru::delete()`/`bulkDelete()` hanya `update(status_aktif=0)` — tidak ada pembersihan baris `kepala_sekolah` yang menunjuk ke guru itu. Dikombinasikan dengan `getSaatIni()` yang tidak filter `status_aktif` (§6.5), efeknya: **kepala sekolah yang dinonaktifkan lewat menu Guru tetap tampil sebagai "Kepala Sekolah saat ini" sampai admin manual klik "Kosongkan" atau tetapkan pengganti.** Keputusan desain saat porting (replikasi apa adanya, atau perbaiki dgn filter status_aktif/auto-kosongkan) perlu diputuskan eksplisit — bukan sekadar bug kecil, mengubah behavior data lama.

### 6.11 Integrasi Sync Hub API (`Commands/SyncPush.php`)
Urutan push (NON-perusahaan saja): `pushWaliKelas`→`pushKepalaSekolah`→`pushJadwalPelajaran`→`pushKalenderAkademik`... Komentar verbatim: "Jadwal Pelajaran, Wali Kelas, Kepala Sekolah BUTUH guru & kelas SUDAH tersinkron dulu... makanya ditaruh PALING TERAKHIR dlm urutan push." Kepala Sekolah **TIDAK DIKIRIM sama sekali** untuk tipe `perusahaan`.

**Field `is_kepala_sekolah` di payload `pushGuru()`** (endpoint `/api/v1/sync/guru`):
```php
$kepsekGuruId = (new KepalaSekolahModel())->getSaatIni()['guru_id'] ?? null; // SEKALI di luar loop
...
'is_kepala_sekolah' => $kepsekGuruId !== null && (int)$g['guru_id'] === (int)$kepsekGuruId,
```
`getSaatIni()` dipanggil **TANPA parameter tahun** → default `activeTahunAjaranId()` (tahun AKTIF saat sync, BUKAN riwayat semua tahun) — penetapan tahun mendatang yang sudah diisi lewat `?tahun=` TIDAK tercermin di `is_kepala_sekolah` sampai tahun itu benar-benar aktif. Perbandingan integer eksplisit `(int)` kedua sisi — TIDAK ada bug MySQLi string-vs-strict di sini.

Versioning fingerprint: field `is_kepala_sekolah` ditambahkan di versi payload **"v3"** (2026-09-04) — bump fingerprint (`v3-full`) supaya PC dengan data "kebetulan" identik ke kiriman v2 tetap kirim ulang sekali. (v4 = tambahan `status_keluar`, lihat `04-infra-auth-sync.md`.)

**`pushKepalaSekolah()` — endpoint terpisah `/api/v1/sync/kepala-sekolah`**:
```php
$rows = (new KepalaSekolahModel())->findAll(); // SEMUA baris, SEMUA tahun ajaran
$payload = array_map(fn($k) => [
    'source_id' => (int)$k['kepala_sekolah_id'],
    'guru_source_id' => (int)$k['guru_id'],
    'tahun_ajaran_source_id' => (int)$k['tahun_ajaran_id'],
], $rows);
$this->post($url,$token,'/api/v1/sync/kepala-sekolah',$payload, true); // full=true
```
**BEDA dari `is_kepala_sekolah` di atas**: endpoint ini kirim **SELURUH riwayat SEMUA tahun ajaran** (bukan cuma aktif), pola identik `pushWaliKelas()`. `full:true` WAJIB — komentar verbatim (merujuk pushWaliKelas): "baris yang TIDAK ikut terkirim (guru dilepas dari wali kelas mana pun di suatu tahun) berarti benar2 dihapus, bukan sebagian data yang kebetulan tidak berubah." Sama untuk kepala sekolah: kalau di tahun tertentu ada kepsek lalu "Kosongkan" (dihapus dari tabel), `full:true` memberi tahu Hub API baris itu memang harus dihapus di server juga.

**Kesimpulan untuk porting**: modul ini punya **DUA representasi sync berbeda** yang harus SAMA-SAMA direplikasi: (1) field `is_kepala_sekolah` di payload guru — snapshot tahun AKTIF saja; (2) payload riwayat lengkap `/sync/kepala-sekolah` — SEMUA baris/tahun, semantik full-replace.

### 6.12 Sidebar menu
`templates/sidebar.php`: label menu verbatim **"Kepala Sekolah"**, item aktif kalau `uri_string()` diawali `kepala-sekolah`.

---

## 7. KELAS — `app/Controllers/Kelas.php`

### 7.1 Helper (private)
`withWaliText($kelas, ?$tahunAjaranId=null)`: menyuntikkan teks wali kelas gabungan (bisa "Nama1 & Nama2" untuk 2+ wali) via `WaliKelasModel::getDisplayText()` ke array `$kelas` untuk ditampilkan di halaman non-index (detail/edit/print) yang masih menampilkan teks itu apa adanya.

### 7.2 `index()` — GET `kelas`
1. `tahunAjaranId=(int)(getGet('tahun')?:activeTahunAjaranId())` — opsional, default aktif; TU bisa isi wali kelas tahun DEPAN dari jauh hari tanpa mengubah tahun berjalan. `aktifId=activeTahunAjaranId()`.
2. `kelasList=getKelasWithCount()` (semua kelas+jumlah siswa aktif per kelas+teks wali gabungan).
3. `perTingkat`: dikelompokkan per tingkat, akumulasi total siswa dan daftar kelas.
4. `tingkatModel=new TingkatModel()`.
5. Render `kelas/index` dengan: `kelasList`, `perTingkat`, `totalSiswa`(sum semua), `tingkatList=getAktif()`, `semuaTingkat=findAll()`(termasuk non-aktif), `labelTingkat=getLabelMap()`, `waliPerKelas=getSemuaDikelompokkan(tahunAjaranId)` (data mentah per-slot, bukan cuma teks gabungan — dibutuhkan bangun form edit), `tahunAjaranId`, `tahunAjaranAktif=aktifId`, `tahunAjaranList=getDropdown()`.

**Catatan arsitektur**: Penetapan Wali Kelas (cari-ketik-klik) DIPINDAH KE SINI dari menu Guru Pengampu 2026-08-27.

### 7.3 `store()` — POST `kelas/store`
1. `tingkat=trim(getPost('tingkat'))`; `namaKelas=formatNamaKelas(tingkat,getPost('nama_kelas'))`.
2. Validasi: `nama_kelas=>'required|min_length[3]|max_length[100]'`, `tingkat=>'required|is_not_unique[tingkat.kode]'` (divalidasi ke MASTER, bukan daftar tetap). Gagal→redirect back `withInput()`+error `implode(' ',getErrors())`.
3. `mb_strlen(namaKelas)>100`→"Nama lengkap kelas maksimal 100 karakter." (setelah prefix ditambahkan bisa melebihi 100).
4. Cek duplikat (tingkat,namaKelas) manual→"Nama kelas tersebut sudah dipakai pada tingkat yang sama. Pulihkan kelas arsip atau gunakan nama lain."
5. `insert(['nama_kelas'=>namaKelas,'tingkat','kelompok'=>null,'is_active'=>1])`.
6. Redirect `kelas` message='Kelas baru berhasil ditambahkan dan akan disinkronkan ke Absen.' (menyiratkan integrasi eksternal ke aplikasi Absen — di luar cakupan tapi teks harus dipertahankan).

### 7.4 `edit(int $id)` — GET `kelas/(:num)/edit`
`find($id)`; 404. Render `kelas/edit` dengan `kelas=withWaliText($kelas)`.

### 7.5 `update(int $id)` — POST `kelas/(:num)/update`
1. `find($id)`; 404.
2. Validasi: `nama_kelas=>'required|min_length[3]|max_length[100]'` (TIDAK ada validasi tingkat — tingkat tidak bisa diubah lewat edit). Gagal→re-render `kelas/edit`+validation.
3. `namaKelas=formatNamaKelas($kelas['tingkat'],getPost('nama_kelas'))`; >100→"Nama lengkap kelas maksimal 100 karakter."
4. Cek duplikat (tingkat,namaKelas) KECUALI diri sendiri→"Nama kelas tersebut sudah digunakan pada tingkat yang sama."
5. `update($id,['nama_kelas'=>namaKelas])` — **`wali_kelas` SENGAJA tidak diterima dari form ini** — satu-satunya tempat mengubahnya adalah menu Wali Kelas (`assignWaliKelas`).
6. Redirect `kelas` message='Data kelas berhasil diperbarui.'

### 7.6 `archive(int $id)` — POST `kelas/(:num)/archive`
1. `find($id)`; tidak ada→"Kelas tidak ditemukan."
2. Hitung siswa aktif kelas ini; >0→TOLAK, "Kelas masih dipakai {n} siswa aktif. Pindahkan/arsipkan siswanya terlebih dahulu."
3. `update($id,['is_active'=>0,'archived_at'=>now()])`. Redirect message='Kelas dipindahkan ke arsip dan akan dinonaktifkan di Absen.'

Komentar: "Kelas tidak dihapus fisik karena student_academics/riwayat presensi masih membutuhkan identitasnya." — kelas TIDAK PERNAH dihapus permanen.

### 7.7 `restore(int $id)` — POST `kelas/(:num)/restore`
`find($id)` tidak ada→"Kelas tidak ditemukan." Else `update($id,['is_active'=>1,'archived_at'=>null])`. Redirect message='Kelas diaktifkan kembali.'

### 7.8 `detail(int $id)` — GET `kelas/(:num)`
`find($id)`; 404. `siswaKelas`=siswa where(kelas_id=$id) urut nama. `siswaTanpaKelas`=siswa where(kelas_id IS NULL) urut nama (kandidat tambah siswa). Render `kelas/detail` dengan `kelas=withWaliText($kelas)`.

### 7.9 `addSiswa(int $id)` — POST `kelas/(:num)/add-siswa`
`find($id)` tidak ada→"Kelas tidak ditemukan." `siswaIds=getPost('siswa_ids')` kosong→redirect ke `kelas/{id}` warning='Tidak ada siswa yang dipilih.' Else loop `update(siswaId,['kelas_id'=>id])` per siswa (TANPA cek duplikat — beda dari Ekskul::addSiswa). Redirect message="{jumlah} siswa berhasil ditambahkan ke kelas."

### 7.10 `removeSiswa(int $kelasId, int $siswaId)` — POST
`update(siswaId,['kelas_id'=>null])` langsung (tanpa cek dulu apakah siswa memang di kelas itu). Redirect message='Siswa berhasil dikeluarkan dari kelas.'

### 7.11 `printKelas(int $id)` — GET
`find($id)` 404. `siswaKelas` urut nama. Render `kelas/print` dengan `kelas=withWaliText($kelas)`.

### 7.12 `printTingkat(string $tingkat)` — GET
Semua kelas 1 tingkat urut nama, gabungkan SEMUA siswanya (nama_kelas disisipkan manual per siswa) jadi 1 daftar cetak. `labelTingkat=getLabelMap()`. Render `kelas/print` dengan `kelas=['nama_kelas'=>'Semua '.label,'tingkat'=>tingkat]` (dummy, BUKAN kelas asli — tidak ada wali_kelas di sini karena mencakup banyak kelas).

### 7.13 `formatNamaKelas(string $tingkat, string $nama): string` (private)
`nama=trim(collapse spasi)`. `prefix=is_numeric($tingkat)?"Kelas {$tingkat}":$tingkat` (tingkat non-numerik seperti "TK-A" dipakai apa adanya TANPA kata "Kelas"). `nama` SUDAH diawali prefix (regex `^prefix\b` case-insensitive)→dipakai apa adanya (tidak double-prefix). Else `trim(prefix.' '.nama)`.

---

## 8. KelasModel (`app/Models/KelasModel.php`)

`table='kelas'`, `primaryKey='kelas_id'`, `returnType='array'`, `useTimestamps=true`.

`allowedFields`: `nama_kelas, tingkat, kelompok, is_active, archived_at`.

`validationRules` bawaan model (default kalau dipanggil tempat lain — controller memvalidasi ulang manual): `nama_kelas=>'required|min_length[3]|max_length[100]'`, `tingkat=>'required|is_not_unique[tingkat.kode]'`. `validationMessages`: nama_kelas.required='Nama kelas wajib diisi.', min_length='Nama kelas minimal 3 karakter.', tingkat.required='Tingkat kelas wajib diisi.', tingkat.is_not_unique='Tingkat tersebut belum terdaftar. Tambahkan dulu di menu Kurikulum & Jam Belajar.'

Method custom:
- `getKelasWithCount()`: LEFT JOIN `siswa s ON s.kelas_id=k.kelas_id AND s.status='aktif'`, GROUP BY k.kelas_id, `total_siswa=COUNT`, urut tingkat ASC,nama_kelas ASC. `wali_kelas` disuntikkan sesudahnya dari `getDisplayTextMap()` (tanpa parameter tahun → default tahun AKTIF, BUKAN tahun yang sedang dipilih di UI — lihat edge-case §12).
- `getDropdown()`: id=>nama_kelas, is_active=1 saja, urut tingkat,nama_kelas.
- `getDropdownByTingkat(?tingkat)`: dibatasi 1 tingkat; kosong→fallback getDropdown() (dipakai PSB/CalonSiswa).
- `getDistinctTingkat()`: tingkat SUNGGUHAN dipakai kelas aktif (dengan label master), urut t.urutan,k.tingkat.
- `getAktifWithTingkat()`: kelas aktif+nama_tingkat,urutan — grid jadwal.

---

## 9. TAHUN AJARAN — `app/Controllers/TahunAjaran.php` + `TahunAjaranModel`

### 9.1 `index()` — GET
`orderBy('nama','DESC')->findAll()`. Render `tahun_ajaran/index`.

### 9.2 `store()` — POST
1. Validasi: `nama=>'required|regex_match[/^\d{4}\/\d{4}$/]|is_unique[tahun_ajaran.nama]'`. Pesan regex_match='Format tahun ajaran tidak valid (harus berupa angka dengan format YYYY/YYYY).' Gagal→redirect back withInput()+error getError('nama').
2. `insert(['nama','is_active'=>0])` — SELALU dibuat NON-aktif.
3. Redirect message='Tahun ajaran berhasil ditambahkan.'

**Catatan UI**: form `tahun_ajaran/index.php` pakai `<select>` opsi tahun 2021 s/d tahun+30 (format "YYYY/YYYY+1") — validasi controller pakai regex bebas tapi praktik UI membatasi rentang.

### 9.3 `setActive(int $id)` — POST
`find($id)` tidak ada→"Tahun ajaran tidak ditemukan." Else `setActive($id)`: TRANSAKSI — nonaktifkan SEMUA (`set(is_active,0)->update()` tanpa where), lalu aktifkan yang dipilih. Redirect message="Tahun ajaran {nama} berhasil diaktifkan."

**PENTING — efek samping TIDAK otomatis**: mengganti TA aktif TIDAK memicu `sinkronJabatanGuru()` ulang — `guru.jabatan` HANYA berubah saat ada EDIT assignment wali kelas di tahun YANG SEDANG AKTIF, bukan otomatis saat tahun aktif diganti. Kalau TA baru diaktifkan dan guru punya assignment wali_kelas DI TAHUN BARU itu dari sebelumnya (diisi jauh hari), `guru.jabatan` TIDAK otomatis refresh sampai ada EDIT baru di Kelola Kelas. **Perilaku APA ADANYA (bukan bug tercatat diperbaiki) — WAJIB direplikasi identik.**

### 9.4 `delete(int $id)` — POST
`find($id)` tidak ada→"Tahun ajaran tidak ditemukan." `is_active===1`→TOLAK "Tidak dapat menghapus tahun ajaran yang sedang aktif." **PENTING**: bahkan untuk TA TIDAK aktif, method ini **SELALU** kembali dengan flash **`error`** (bukan `message`) PERSIS: "Tahun ajaran {nama} dipertahankan sebagai arsip historis dan tidak dapat dihapus." — **tidak ada baris kode yang benar-benar menjalankan penghapusan**. Tombol "Hapus" di UI SELALU gagal by design. **WAJIB direplikasi**: tombol ada+bisa diklik (konfirmasi SweetAlert), hasil akhir SELALU pesan error di atas.

### 9.5 `TahunAjaranModel`
`allowedFields`: nama, is_active. `validationRules`: `nama=>'required|min_length[4]|max_length[20]'` (LEBIH LONGGAR dari rule controller store() yang wajib regex YYYY/YYYY — model tidak dipakai langsung karena store() divalidasi manual di controller). `getActive()`: where(is_active,1)->first(). `setActive($id)`: lihat 9.3. `getDropdown()`: id=>nama, urut nama DESC.

---

## 10. EKSKUL — `app/Controllers/Ekskul.php` + `EkskulModel` + `EkskulSiswaModel`

Mekanisme ABSENSI ekskul SENGAJA BELUM ADA (ditunda user). Halaman ini murni kelola daftar ekskul+peserta (rekap), TIDAK ADA fitur presensi.

### 10.1 `index()` — GET
`ekskulList=getEkskulWithCount()` (semua, termasuk arsip). `totalEkskul`=jumlah is_active===1 (dari `$ekskulList` via array_filter). `totalPeserta`=array_sum(total_peserta) SEMUA ekskul (termasuk arsip).

### 10.2 `store()` — POST
Validasi inline: `nama=>'required|min_length[3]|max_length[100]'`. Gagal→redirect back withInput()+error. `insert(['nama'=>trim,'pembina'=>trim?:null,'hari'=>trim?:null,'deskripsi'=>trim?:null,'is_active'=>1])`. Redirect message='Ekskul baru berhasil ditambahkan.'

### 10.3 `edit(int $id)` — GET
`find($id)` 404. Render `ekskul/edit`.

### 10.4 `update(int $id)` — POST
`find($id)` 404. Validasi via model rules (`nama=>'required|min_length[3]|max_length[100]'`). Gagal→re-render+validation. `update($id,['nama'=>trim,'pembina'=>trim?:null,'hari'=>trim?:null,'deskripsi'=>trim?:null])` — **`is_active` TIDAK disentuh** (hanya lewat archive/restore terpisah). Redirect message='Data ekskul berhasil diperbarui.'

### 10.5 `archive(int $id)` — POST
`find($id)` tidak ada→"Ekskul tidak ditemukan." `update($id,['is_active'=>0,'archived_at'=>now()])`. Redirect message='Ekskul dipindahkan ke arsip.' Komentar: "riwayat peserta tetap utuh (tidak dihapus)."

### 10.6 `restore(int $id)` — POST
`find($id)` tidak ada→"Ekskul tidak ditemukan." `update($id,['is_active'=>1,'archived_at'=>null])`. Redirect message='Ekskul diaktifkan kembali.'

### 10.7 `detail(int $id)` — GET
`find($id)` 404. Render `ekskul/detail` dengan `peserta=getPesertaByEkskul($id)`, `siswaBelumIkut=getSiswaBelumIkut($id)`.

### 10.8 `addSiswa(int $id)` — POST
`find($id)` tidak ada→"Ekskul tidak ditemukan." `siswaIds=getPost('siswa_ids')` kosong→redirect `ekskul/{id}` warning='Tidak ada siswa yang dipilih.' Loop tiap siswaId: **cek dulu apakah SUDAH terdaftar** sebelum insert — DISENGAJA (bukan mengandalkan UNIQUE-key error, supaya form disubmit dobel/lambat tidak berhenti di tengah loop). Belum ada→insert `{ekskul_id,siswa_id,tanggal_daftar=today,created_at=now}`. Redirect message="{jumlah} siswa berhasil ditambahkan sebagai peserta." (`jumlah`=count($siswaIds) yang DIKIRIM, BUKAN jumlah yang benar-benar diinsert — kalau ada yang sudah terdaftar & di-skip, pesan tetap melaporkan jumlah asli dicentang).

### 10.9 `removeSiswa(int $ekskulId, int $siswaId)` — POST
`where(ekskul_id,siswa_id)->delete()` langsung. Redirect message='Siswa berhasil dikeluarkan dari ekskul.'

### 10.10 `EkskulModel`
`allowedFields`: nama,pembina,hari,deskripsi,is_active,archived_at. `validationRules`: `nama=>'required|min_length[3]|max_length[100]'`. `getEkskulWithCount()`: LEFT JOIN ekskul_siswa, GROUP BY ekskul_id, `total_peserta=COUNT(es.id)` (**COUNT SEMUA baris, TIDAK memfilter siswa status aktif** — beda dari `getPesertaByEkskul()` yang FILTER status='aktif'. Angka total_peserta di ringkasan/tabel index BISA lebih besar dari jumlah tampak di detail peserta kalau ada siswa lulus/pindah/keluar tapi masih tercatat anggota ekskul — perilaku apa adanya, edge-case). Urut is_active DESC,nama ASC.

### 10.11 `EkskulSiswaModel`
`allowedFields`: ekskul_id,siswa_id,tanggal_daftar,created_at. `useTimestamps=false` (tanpa updated_at). `getPesertaByEkskul($ekskulId)`: JOIN siswa (INNER, filter status='aktif')+LEFT JOIN kelas, urut nama — kolom ekskul_siswa_id(alias es.id),siswa_id,nama,nis,kelas_id,nama_kelas,tanggal_daftar. `getSiswaBelumIkut($ekskulId)`: siswa aktif BELUM ada di ekskul_siswa untuk ekskul ini (whereNotIn), urut nama.

---

## 11. WaliKelasModel — Detail lengkap (`app/Models/WaliKelasModel.php`)

Model TERPENTING modul ini — menjaga SEMUA invarian relasi guru↔kelas↔tahun.

`allowedFields`: kelas_id,guru_id,tahun_ajaran_id,urutan.

- **`activeTahunAjaranId(): int`** — throw `RuntimeException('Tidak ada tahun ajaran aktif - set dulu lewat menu Tahun Ajaran.')` kalau tidak ada TA is_active=1 (dijaga "mustahil" secara desain, fail-loud daripada diam-diam pakai null/0).
- **`getByKelas(kelasId,?tahunAjaranId)`**: daftar wali 1 kelas 1 tahun, urut urutan ASC. Default tahun aktif.
- **`getSemuaDikelompokkan(?tahunAjaranId)`**: semua kelas AKTIF sekaligus, dikelompokkan per kelas_id (hindari N+1).
- **`getKelasByGuru(guruId,?tahunAjaranId)`**: kelas yang SEDANG diampu 1 guru sbg wali DI 1 TAHUN (guru cuma boleh 1 kelas/tahun krn UNIQUE), atau null.
- **`getDisplayText(kelasId,?tahunAjaranId)`**: gabungan nama wali jadi 1 teks (" & " separator), atau null kalau kosong.
- **`getDisplayTextMap(?tahunAjaranId)`**: peta kelas_id=>teks gabungan banyak kelas sekaligus.
- **`tetapkan(kelasId,slot,?guruId,?tahunAjaranId)`**: tunjuk/ganti/kosongkan wali 1 SLOT tertentu 1 tahun.
  - Cari slotLama (kelas+tahun+urutan)→kalau ada, guru lama dicatat sbg terdampak.
  - `guruId!==null`: cek `existing=getKelasByGuru(guruId,ta)` — ADA & kelas BEDA→`kelasLamaDilepas=existing['nama_kelas']`. Lalu **HAPUS guru ini dari MANA PUN DI TAHUN INI** (mencegah bentrok UNIQUE(guru_id,ta)); SENGAJA dibatasi where tahun_ajaran_id supaya assignment tahun LAIN tidak ikut terhapus.
  - Hapus baris slot lama.
  - `guruId!==null`→insert baru.
  - **`sinkronJabatanGuru($terdampak)` HANYA dipanggil kalau `$tahunAjaranId===activeTahunAjaranId()`** — edit assignment tahun BUKAN aktif TIDAK BOLEH ubah jabatan hari ini.
  - Return `{kelasLamaDilepas:?string}`.
- **`tetapkanSemua(kelasId,guruIds[],?tahunAjaranId)`**: tunjuk SEMUA wali 1 kelas sekaligus (urutan array=urutan slot, jumlah TIDAK dibatasi). Baris sebelumnya ada DI TAHUN ITU tapi tidak ikut dikirim lagi=dikosongkan.
  - `dipakai`=unique+filter(>0) dari guruIds. `lama`=guru_id existing sebelum perubahan.
  - Per guruId di dipakai: cek existing(kelas lain tahun sama)→catat dilepasDari[guruId]; hapus SEMUA baris guru itu DI TAHUN INI.
  - Hapus SEMUA baris kelas ini DI TAHUN INI, insert ulang urut 1..N sesuai urutan array dipakai.
  - `sinkronJabatanGuru(array_merge(lama,dipakai))` HANYA kalau tahun diedit=tahun aktif.
  - Return `dilepasDari` (guru_id=>nama_kelas_lama).
- **`sinkronJabatanGuru(guruIds[])` (private)**: per guruId unik: `jabatanBaru=getKelasByGuru(guruId,aktif)!==null?'guru_kelas':'guru_bidang'`; `UPDATE guru SET jabatan=$jabatanBaru WHERE guru_id=$guruId AND jabatan!='karyawan'` — **Karyawan SENGAJA TIDAK PERNAH disentuh** (guard di WHERE, bukan PHP).

---

## 12. Perilaku Halus / Edge Case yang Mudah Terlewat (WAJIB dipertahankan identik)

1. **AJAX Guru Pengampu — 2 revisi berturut demi UX**: akar masalah BUKAN scroll — kode lama memaksa baris keluar mode edit otomatis (tinggi mengecil mendadak) dan mapel baru masih full-reload. Fix final: JANGAN ubah tampilan apa pun secara visual setelah Simpan sukses via AJAX.

2. **Wali Kelas AJAX autosave tanpa tombol Simpan**: sejak 2026-08-27 TIDAK ADA tombol Simpan — pilih/hapus nama langsung fetch() POST tersimpan seketika. Alasan: `cariGuruKelas()` baca DB SUNGGUHAN, tanpa autosave guru yang baru dilepas di layar tetap dianggap "sudah dipakai" (bug nyata dilaporkan user). Sukses TANPA notifikasi; kegagalan teks kecil `.js-wali-error` tanpa reload/popup.

3. **BUG NYATA "Tiara Puspita" (2026-08-27)** — filter `cariGuruKelas()` dulu HANYA `where('jabatan','guru_kelas')` → semua guru_bidang (termasuk yang sungguhan sedang jadi wali) tidak muncul. Diperbaiki `whereIn(['guru_kelas','guru_bidang'])`. Bug bergandengan: guard `Guru::update()` yang menolak ubah jabatan guru sedang jadi wali HANYA cek `jabatan==='guru_kelas'` sebagai syarat awal — Guru Bidang yang sungguhan jadi wali bisa lolos diam-diam diubah jadi Karyawan. Diperbaiki jadi jabatan-agnostic: cek `$waliDariSaatIni!==null`.

4. **BUG NYATA cast integer di `cariGuruKelas()`**: MySQLi mengembalikan guru_id sebagai STRING; `in_array(...,true)` strict tanpa `array_map('intval',...)` membuat filter "sudah jadi wali" TIDAK PERNAH bekerja. WAJIB cast eksplisit sebelum strict-compare — di .NET/C# otomatis type-safe tapi filosofinya (jangan bandingkan tipe beda implisit) tetap relevan saat porting logic pencarian serupa.

5. **`ajax=1` sebagai query param eksplisit** (`Guru::index()`), BUKAN deteksi header/isAJAX() — sengaja "jelas & mudah diuji lewat curl". `?ajax=1` mengembalikan HANYA fragmen `guru/_hasil`.

6. **`no_handphone_unik_aktif` — 2 lapis perbaikan bug sama** (2026-09-07): `is_unique` bawaan CI4 cek SELURUH baris termasuk arsip → nomor HP orang diarsipkan terkunci selamanya. Rule custom cek HANYA status_aktif=1. TAPI unique index DB (`uniq_guru_no_handphone`) tetap mengunci semua baris tanpa peduli status — ditemukan lewat test import upsert nyata (INSERT baru pakai ulang HP arsip GAGAL total "Duplicate entry" dari MySQL, membatalkan seluruh transaksi walau validasi form lolos). Solusi akhir: DROP unique index dari DB, keunikan MURNI di aplikasi. **Untuk .NET/SQLite: JANGAN buat UNIQUE constraint di `no_handphone` level database — keunikan HARUS dicek di kode aplikasi, scoped ke status_aktif=1 saja.**

7. **Rule custom CI4 harus SELALU dipanggil berkurung `[param]`**, termasuk create dengan literal "0" (jangan kecualikan siapa pun) — detail implementasi framework CI4, tidak relevan teknis di .NET, tapi maknanya (keunikan no_handphone SELALU dicek ulang server-side CREATE maupun UPDATE, exclude-self saat update) harus tetap identik.

8. **Migrasi `guru_mata_pelajaran` unique key — 2 tahap perbaikan**: key lama 2-kolom (guru_id,mapel_id) DIBIARKAN hidup berdampingan dulu (MySQL menolak drop selagi menopang FK) — TERNYATA masih aktif membatasi 1 guru 1 baris per mapel, padahal fitur tingkat dibuat supaya guru+mapel sama boleh >1 baris kalau tingkat beda — baris ke-2 selalu ditolak "Duplicate entry" palsu. Diperbaiki drop key lama di migrasi terpisah. **Untuk SQLite: UNIQUE constraint tabel relasi guru↔mapel HARUS PERSIS (guru_id,mata_pelajaran_id,tingkat) — TIDAK ADA constraint tambahan (guru_id,mata_pelajaran_id) saja.**

9. **Guard perubahan jabatan guru sedang jadi wali kelas** (`Guru::update()`): `$waliDariSaatIni!==null` dan jabatan baru BUKAN guru_kelas/guru_bidang→TOLAK pesan PERSIS: "{nama} masih tercatat sbg wali kelas {nama_kelas}. Tetapkan wali kelas pengganti dulu di menu Kelola Kelas sebelum mengubah jabatannya."

10. **Sinkronisasi jabatan guru HANYA berlaku TA AKTIF** (`tetapkan()`/`tetapkanSemua()`): kondisi `if($tahunAjaranId===activeTahunAjaranId())` sebelum panggil `sinkronJabatanGuru()`. Edit assignment wali kelas TA belum aktif TIDAK mengubah `guru.jabatan` hari ini.

11. **`TahunAjaran::setActive()` TIDAK memicu sinkronisasi jabatan otomatis** — hanya UPDATE is_active, tidak panggil `sinkronJabatanGuru()`. Kalau TA baru sudah punya data wali_kelas diisi jauh hari sebelum diaktifkan, `guru.jabatan` TIDAK otomatis refresh saat TA diaktifkan — baru berubah kalau ada EDIT baru setelah TA jadi aktif. Perilaku APA ADANYA — replikasikan identik kecuali diminta ubah eksplisit.

12. **`TahunAjaran::delete()` SELALU gagal, tidak pernah benar-benar menghapus** — bahkan TA tidak aktif, hanya mengembalikan error "Tahun ajaran {nama} dipertahankan sebagai arsip historis dan tidak dapat dihapus." tanpa baris penghapusan apa pun. Tombol Hapus tetap ada di UI (SweetAlert) tapi selalu gagal. WAJIB direplikasi apa adanya.

13. **`getKelasWithCount()` memakai `getDisplayTextMap()` TANPA parameter tahun** — kolom "Wali Kelas" di tabel ringkasan SELALU tampilkan wali TAHUN AKTIF, terlepas dari selector `?tahun=` yang dipilih user (selector hanya mempengaruhi `waliPerKelas` untuk form edit combobox). Perlu verifikasi ulang saat porting apakah field ini masih dipakai langsung — `withWaliText()` yang dipanggil dari `detail()`/`edit()`/`printKelas()` JUGA dipanggil TANPA argumen tahun eksplisit, sehingga SELALU default tahun aktif juga. Hanya `kelas/index.php` sendiri yang correctly tahun-aware, lewat `waliPerKelas`.

14. **`getAktifWithKelas()`/`getArsipWithKelas()` JOIN ke wali_kelas tanpa filter tahun ajaran** — karena UNIQUE(guru_id,ta) mengizinkan 1 guru punya baris di BEBERAPA tahun berbeda, JOIN tanpa WHERE tahun berpotensi menggandakan baris guru pada LEFT JOIN kalau guru tercatat wali di >1 tahun bersamaan. APA ADANYA di kode sumber — perlu keputusan eksplisit saat porting apakah dianggap bug untuk diperbaiki atau dipertahankan.

15. **Import Guru: kolom Jabatan HANYA 2 pilihan sejak 2026-08-27** — status Guru Kelas vs Guru Bidang TIDAK bisa diketik lewat import lagi. Nilai lama "Guru Kelas"/"Guru Bidang" (kompatibilitas) tetap diterima sebagai sinonim tapi KEDUANYA jadi 'guru_bidang' (BUKAN 'guru_kelas').

16. **Import upsert Guru: kunci pencocokan No HP HANYA dibanding baris status_aktif=1** — konsisten `no_handphone_unik_aktif`. Nomor HP arsip bebas dipakai ulang (INSERT baru bukan UPDATE ke arsip). Sel kosong pada baris koreksi=jangan diubah. `status_aktif`/`kelas_id` SENGAJA tidak pernah disentuh lewat import.

17. **`GuruMataPelajaran` — tautan baru selalu UNIVERSAL (tingkat=null) lewat import** — batasi tingkat manual setelah import. Sama untuk import Wali Kelas (selalu isi slot 1, TA AKTIF, bukan tahun lain).

18. **`Guru.php` — field opsional kosong SELALU null (bukan string kosong)** via `trim((string)getPost(...))?:null` — konsisten mata_pelajaran, gelar_terakhir, nip, jenis_kelamin. `no_handphone` di-strip ke digit murni via `preg_replace('/\D+/','',...)`.

19. **`formatNamaKelas()` mencegah double-prefix** — nama sudah diawali prefix tingkat→tidak ditambah lagi (regex `^prefix\b` case-insensitive). Tingkat non-numerik (mis. "TK-A")→prefix=kode tingkat itu sendiri (BUKAN "Kelas TK-A").

20. **`EkskulModel::getEkskulWithCount()` menghitung SEMUA baris ekskul_siswa tanpa filter status siswa** — beda dari `getPesertaByEkskul()` yang cuma hitung status='aktif'. Angka total_peserta di ringkasan bisa lebih besar dari yang tampak di detail peserta.

21. **`Ekskul::addSiswa()` cek duplikat manual sebelum insert** (bukan mengandalkan UNIQUE exception) — form disubmit dobel/lambat tidak berhenti di tengah loop. Pesan sukses melaporkan JUMLAH DICENTANG, bukan jumlah yang benar-benar diinsert. Perilaku IDENTIK `Kelas::addSiswa()` — TAPI `Kelas::addSiswa()` TIDAK ADA pengecekan duplikat sama sekali (langsung update per siswa tanpa cek), karena tidak ada konsep "sudah anggota" untuk kelas (1 siswa 1 kelas, overwrite langsung aman).

22. **Kepala Sekolah: setelah `tetapkan()` sukses via AJAX, JS `window.location.reload()`** — BEDA dari pola Wali Kelas/Guru Pengampu yang eksplisit menghindari reload. SATU-SATUNYA endpoint AJAX di ketiga modul yang tetap reload penuh — pertahankan perbedaan ini, JANGAN disamakan pola "tanpa reload" karena tidak ada catatan histori bug UX untuk Kepala Sekolah seperti 2 modul lainnya.

23. **`tahun_ajaran_mulai_id` di `guru`**: kolom ADA di skema (komentar rencana "menyusul di form create/edit"), TAPI TIDAK PERNAH muncul di form manapun dan tidak ada di allowedFields model — secara efektif SELALU NULL untuk semua guru. Untuk porting: bisa diabaikan dulu KECUALI ada bukti pemakaian di modul lain — cek ulang saat spec modul lain dibuat.

24. **Checkbox `status_aktif` form edit Guru**: HTML checkbox tidak dicentang=TIDAK mengirim value sama sekali — `getPost('status_aktif')!==null?1:0` bergantung perilaku HTTP standar ini. Penting untuk porting ASP.NET Core (Blazor/MVC) yang biasanya render hidden input tambahan `false` — perlu disesuaikan supaya semantik identik: TIDAK dicentang=nonaktifkan.

25. **`Guru Pengampu` badge jabatan HANYA 2 label** (`jabatan==='guru_kelas'?'Guru Kelas':'Guru Bidang'`, ternary tanpa cek karyawan eksplisit — TAPI `getGuruWithMapel()` sudah filter whereIn(guru_kelas,guru_bidang), jadi baris karyawan memang tidak pernah muncul, ternary aman).

---

## 13. Ringkasan View — Field Form, Tombol, Teks Verbatim

### 13.1 `guru/index.php` + `guru/_hasil.php`
Page actions: "Print"(feather-printer, `guru/print` _blank), "Import Excel"(feather-upload, HANYA kalau !isPerusahaan), "Tambah Pegawai"/"Tambah Guru"(feather-plus, primary).
Alert import_errors: "Import selesai dengan beberapa baris yang dilewati:" + list.
Kartu ringkasan: perusahaan 1 kartu "Total Pegawai"; pendidikan 4 kartu Total/Wali Kelas/Guru Bidang/Karyawan.
Form cari placeholder "Cari nama, NIP, atau No. HP...", "Bersihkan"(muncul kalau keyword terisi).
Select "Tampilkan": 50/100/150/200 (disembunyikan saat search).
Toolbar bulk delete: "{N} guru/pegawai dipilih", "Hapus Terpilih".
Tabel: checkbox,No,Nama(link),JK(badge/-,[Jabatan,Kelas/Wali—pendidikan],No.HP,Aksi(Detail+Hapus).
Label jabatan: guru_kelas→"Guru Kelas", guru_bidang→"Guru Bidang", karyawan→"Karyawan".
Empty: search→"Tidak ada guru/pegawai yang cocok dengan pencarian \"{keyword}\"."; else→"Belum ada data guru/pegawai. Tambah sekarang."
SweetAlert Bulk Delete: "Hapus {N} Data Guru?"/"{N} data guru/pegawai yang dipilih akan dihapus secara permanen." input select alasan keluar (''→"-- Tidak dipilih --", purna_bakti→"Purna Bakti / Pensiun", resign→"Resign / Mengundurkan Diri", diberhentikan→"Diberhentikan"). "Ya, Hapus Semua"/"Batal".
SweetAlert Hapus Satu: "Hapus Guru?"/"Data guru {nama} akan dihapus secara permanen." sama pilihan. "Ya, Hapus"/"Batal". **CATATAN**: teks "dihapus secara permanen" padahal aksi HANYA arsip — teks menyesatkan tapi WAJIB dipertahankan verbatim.
JS pencarian: debounce 300ms, `?ajax=1`, replace `#hasilGuru`, history.replaceState.

### 13.2 `guru/create.php`
Judul: "Tambah Pegawai"/"Tambah Data Guru / Pegawai". Field: Nama Lengkap*(regex nama), Jenis Kelamin(opsional), NIP(opsional), Jabatan*(select Guru/Karyawan, HANYA tampil non-perusahaan; perusahaan→hidden 'karyawan') dengan teks bantu "Status Guru Kelas/Guru Bidang serta wali kelas ditentukan OTOMATIS setelah ini disimpan, lewat menu Kelola Kelas — bukan di form ini.", Mata Pelajaran(opsional, "Catatan bebas saja — penugasan mapel resmi diatur di menu Guru Pengampu."), Gelar Terakhir(opsional), No.Handphone*(required), Alamat(opsional). Tombol "Simpan Data Pegawai"/"Simpan Data Guru", "Batal".

### 13.3 `guru/detail.php`
Card "Status Penugasan" (hanya bukan perusahaan & jabatan≠karyawan): Wali Kelas(badge/"— (bukan guru/staf pengajar)"/"Belum menjadi wali kelas manapun"), Jam Mengajar(TA aktif)—"Belum ada tahun ajaran aktif"/"Belum ada jam terjadwal"/"{total} JP (Ganjil {n}, Genap {n})". Teks: "Dihitung otomatis dari Jadwal Pelajaran — bukan angka yang diketik manual..."
Form Detail/Edit: Nama*,Jenis Kelamin,NIP,Jabatan(select, teks bantu sama create bila guru_kelas/guru_bidang),Mata Pelajaran,Gelar Terakhir,No.Handphone*,Alamat,checkbox "Aktif". Tombol "Simpan Perubahan","Kembali".

### 13.4 `guru/arsip.php`
Tab switcher Arsip Siswa/Arsip Guru. Judul "Arsip Pegawai"/"Arsip Guru & Pegawai". "Data yang sudah dinonaktifkan (bukan di-hapus). Buka Detail lalu centang \"Aktif\" untuk memulihkan." Tabel sama index tanpa checkbox/perpage, aksi="Pulihkan". Empty: "Belum ada guru/pegawai yang diarsipkan."

### 13.5 `guru/import.php`
Alert: "Urutan kolom wajib tetap: Nama | Jenis Kelamin (opsional) | Jabatan (opsional) | Gelar Terakhir (opsional) | No HP". Daftar aturan termasuk: "File ini bisa berisi campuran data baru & koreksi data lama sekaligus - kalau No HP di suatu baris sudah dipakai data yang MASIH AKTIF, baris itu otomatis dianggap koreksi (kolom yang dikosongkan tidak akan diubah). No HP milik data yang sudah diarsipkan tetap bebas dipakai ulang untuk data baru." dan "Sebelum benar-benar tersimpan, akan ada halaman Preview..." Tombol "Download Template XLSX". Form upload required, "Import Sekarang"/"Batal".

### 13.6 `guru/preview_import.php`
Badge: "{N} Baru"(success),"{N} Diperbarui"(warning),"{N} Error"(danger, >0). Teks: "Periksa dulu hasil klasifikasi... Baris Diperbarui akan MENGOREKSI data yang sudah ada (No HP cocok dgn data yang masih aktif) - kolom yang tidak diisi di Excel TIDAK akan diubah." Tabel: Baris,No HP,Nama,Status(badge),Perubahan(diff, jabatan label ditranslasi: karyawan→"Karyawan", else→"Guru"). Tombol "Terapkan Sekarang ({N} baris)"/"Batal".

### 13.7 `guru/print.php`
Halaman cetak polos. Header "SDIT AL IKHLAS 86"/"Daftar Data Pegawai"/"Daftar Data Guru & Pegawai". Tombol "🖨 Cetak Sekarang"/"✕ Tutup"(no-print). Kolom: No,Nama,NIP,JK,[Jabatan,Kelas/Wali,Mata Pelajaran—bukan perusahaan],No.HP,Alamat. Footer total.

### 13.8 `penugasan_mengajar/index.php` ("Guru Pengampu")
Judul "Guru Pengampu". Tombol Import Excel. Tabel: Nomor(badge/input tersembunyi),Nama+label jabatan kecil,Mata Pelajaran(list+hapus(x) saat edit+"+ Tambah mata pelajaran"),Tingkat(label per mapel/"Semua Tingkat"),Aksi(Edit↔Batal toggle+Simpan saat edit+area error kecil merah). Empty: "Belum ada guru aktif (Guru Kelas/Guru Bidang)."/"Belum ada mata pelajaran. Tambahkan dulu di menu Kurikulum & Jam Belajar." SweetAlert hapus tautan: "Hapus Tautan Ini?"/"Tautan ke {nama} akan dihapus."

### 13.9 `penugasan_mengajar/import_wali_kelas.php` / `import_guru_mapel.php`
Alert kolom wajib + aturan (lihat §5.6/5.7). Tombol download template (isi otomatis nama existing). Form upload+"Import Sekarang"/"Batal".

### 13.10 `kepala_sekolah/index.php`
Teks: "Kepala Sekolah BUKAN jabatan terpisah - orang yang ditunjuk di sini TETAP guru/guru kelas/pegawai seperti biasa..., cuma dapat kapasitas tambahan menyetujui izin guru di aplikasi Mobile-app." Select TA (opsi "{nama} (aktif sekarang)" utk aktif). Alert (bukan TA aktif): "Anda sedang melihat/mengisi tahun ajaran yang BUKAN tahun aktif sekarang..." Blok "Kepala Sekolah saat ini"(nama+tombol Kosongkan) atau "Belum ada Kepala Sekolah ditetapkan." Form "Ganti/Tetapkan": input autocomplete "Ketik nama guru/pegawai...". SweetAlert kosongkan: "Kosongkan Kepala Sekolah?" *(detail lebih lanjut perlu verifikasi ulang dari kode sumber — lihat peringatan §6)*

### 13.11 `kelas/index.php`
Panel "Kelola Tingkat"(collapse): form tambah(Kode*,Nama Tampilan*,Urutan,"Tambah"), tabel edit massal(nama,urutan,checkbox aktif,hapus per baris SweetAlert "Hapus Tingkat?"), "Simpan Perubahan Tingkat".
Form "Tambah Kelas": select Tingkat("-- Belum ada tingkat --" kalau kosong), Nama Kelas*(placeholder "Contoh: Al Mughni"), "Tambah Kelas". Hint: "Awalan tingkat dibuat otomatis, misalnya tingkat 1 dan nama Al Mughni menjadi Kelas 1 Al Mughni. Kelas yang tidak dipakai lagi diarsipkan agar riwayat tetap utuh."
Kartu statistik: Total Kelas, Total Siswa Terdaftar di Kelas.
Selector TA Wali Kelas: "Wali Kelas Tahun Ajaran", badge "Bukan tahun aktif - perubahan tidak memengaruhi tahun berjalan" (bukan aktif).
Daftar per Tingkat (collapsible): header badge total siswa+tombol Print. Tabel per kelas: Nama Kelas, **Wali Kelas**(combobox multi-slot autocomplete+clear(x)+tambah(+), TANPA tombol Simpan—autosave), Jumlah Siswa(badge), Status(Aktif/Arsip), Aksi(Detail,Print,Arsipkan/Pulihkan).
Konfirmasi arsip: native `confirm('Arsipkan kelas ini?')` (BUKAN SweetAlert — beda dari kebanyakan konfirmasi lain, perhatikan saat porting UI).
Auto-expand tingkat+scroll `?expand=...#kelas-N` setelah redirect assignWaliKelas.

### 13.12 `kelas/edit.php`
Nama Kelas*("Awalan tingkat ditambahkan otomatis. Tingkat tidak dapat diubah."), Wali Kelas(readonly disabled, "Diatur dari menu Wali Kelas & Mata Pelajaran, bukan di sini." link penugasan-mengajar), Tingkat(readonly, format "Kelas {tingkat} SD" — **HARDCODE "SD" walau tingkat bisa TK/Playgroup**, kemungkinan teks lama belum update; kutip verbatim apa adanya). "Simpan"/"Batal".

### 13.13 `kelas/detail.php`
Info: "Tingkat {tingkat}{' SD' jika numeric} • Wali Kelas: {nama} • {N} siswa". Tabel siswa+tombol keluarkan(SweetAlert "Keluarkan dari Kelas?"). Form tambah siswa: checkbox list siswa tanpa kelas+"Pilih Semua"/"Batal Pilih" toggle.

### 13.14 `kelas/print.php`
Header "Daftar Siswa — {nama_kelas}", "Tingkat {t} SD | Wali Kelas: {nama} | Dicetak: ...". Tabel: No,NIS,Nama,JK,Tempat/Tgl Lahir,Nama Ayah,No.HP.

### 13.15 `tahun_ajaran/index.php`
Select dropdown tahun 2021 s/d tahun+30 ("YYYY/YYYY+1"), "Simpan". Tabel: No,Tahun Ajaran,Status(badge),Aksi(Aktifkan+Hapus non-aktif; "Sedang Aktif" utk aktif). SweetAlert Aktifkan: "Aktifkan Tahun Ajaran?"/"Tahun ajaran {nama} akan diaktifkan. Tahun ajaran lain akan dinonaktifkan." SweetAlert Hapus: "Hapus Tahun Ajaran?"/"Tahun ajaran {nama} akan dihapus permanen." — **PENTING: teks menjanjikan penghapusan permanen, TAPI backend SELALU menolak (§12 poin 12) — WAJIB dipertahankan verbatim**.

### 13.16 `ekskul/index.php`
Form tambah: Nama Ekskul*,Pembina,Hari Latihan,Deskripsi(textarea),"Tambah Ekskul". Kartu: "Ekskul Aktif","Total Peserta (Seluruh Ekskul)". Tabel: Nama Ekskul(link),Pembina,Hari,Peserta(badge),Status(badge, arsip class `table-secondary`),Aksi(Lihat Peserta,Edit,Arsipkan/Pulihkan). Empty: "Belum ada ekskul. Tambahkan lewat form di atas."

### 13.17 `ekskul/edit.php`
Nama Ekskul*,Pembina,Hari Latihan,Deskripsi. "Simpan"/"Batal".

### 13.18 `ekskul/detail.php`
Info: Pembina,Hari,jumlah peserta,deskripsi. Tabel peserta: No,NIS,Nama(link),Kelas,Terdaftar,Aksi(keluarkan SweetAlert "Keluarkan dari Ekskul?"). Form tambah: checkbox siswa belum ikut+"Tambahkan"/"Pilih Semua". Teks: "Pilih siswa yang belum mengikuti {nama}. 1 siswa boleh ikut lebih dari 1 ekskul." Empty: "Semua siswa aktif sudah terdaftar di ekskul ini."

---

## 14. Ringkasan Cepat: Yang WAJIB direplikasi identik (checklist implementasi C#)

1. Skema tabel §1 (termasuk constraint yang SUDAH TIDAK ADA di DB, seperti `guru.no_handphone`).
2. Aturan validasi PERSIS kata-per-kata di §3.4, §7.3/7.5, §9.2, §10.2/10.4.
3. Logic derivasi otomatis jabatan guru_kelas↔guru_bidang (§11 sinkronJabatanGuru) — TIDAK PERNAH input manual.
4. Scoping per tahun ajaran Wali Kelas & Kepala Sekolah + "sinkron jabatan hanya kalau tahun diedit=tahun aktif" (§12 poin 10-11).
5. Perilaku AJAX tanpa-reload di Guru Pengampu (updateBaris) dan Wali Kelas (assignWaliKelas) — TANPA notifikasi sukses, TANPA toggle keluar mode edit otomatis. **KECUALI Kepala Sekolah yang justru reload (§12 poin 22) — jangan disamakan.**
6. `TahunAjaran::delete()` SELALU gagal (§12 poin 12); `setActive()` TIDAK memicu sinkronisasi jabatan (§12 poin 11).
7. Keunikan No HP guru HANYA terhadap status_aktif=1 — TANPA unique constraint level database (§12 poin 6).
8. Import Excel 3-langkah (preview session→apply) Guru, klasifikasi insert/update via No HP aktif, partial update (§3.12).
9. Semua teks pesan sukses/error/konfirmasi verbatim §3,§5-10,§13 (termasuk yang menyesatkan seperti "dihapus secara permanen" utk arsip, "akan dihapus permanen" utk TA yang tak pernah terhapus).
10. Pembatasan `isPerusahaan` yang mempersempit jabatan ke karyawan saja dan memblokir seluruh modul Kelas/Ekskul/TahunAjaran/Penugasan/KepalaSekolah untuk instalasi Perusahaan.
11. Modul Kepala Sekolah (§6, lengkap): scoping per tahun ajaran, TANPA guard silang di `Guru.php` (gap nyata dicatat §6.10 — putuskan eksplisit saat porting apakah direplikasi apa adanya atau diperbaiki), reload penuh setelah `tetapkan()` sukses (§6.9, beda dari Wali Kelas/Guru Pengampu), dan DUA representasi sync berbeda ke Hub API (§6.11: snapshot tahun aktif via `is_kepala_sekolah` vs riwayat lengkap via endpoint terpisah).

---

Sumber dibaca (path absolut, tidak ada perubahan dilakukan pada file-file ini):
- `app/Controllers/Guru.php, Kelas.php, Ekskul.php, TahunAjaran.php, PenugasanMengajar.php, KepalaSekolah.php`
- `app/Models/GuruModel.php, KelasModel.php, TahunAjaranModel.php, WaliKelasModel.php, EkskulModel.php, EkskulSiswaModel.php, TingkatModel.php(potongan), MataPelajaranModel.php(potongan), KepalaSekolahModel.php`
- `app/Validation/GuruValidationRules.php`
- `app/Views/guru/*, kelas/*, ekskul/*, tahun_ajaran/*, penugasan_mengajar/*, kepala_sekolah/index.php`
- `app/Database/Migrations/` — semua migrasi terkait guru/kelas/tahun_ajaran/wali_kelas/kepala_sekolah/ekskul/tingkat/guru_mata_pelajaran
- `app/Commands/SyncPush.php` (potongan pushGuru/pushKepalaSekolah, untuk konfirmasi field is_kepala_sekolah)
- `app/Views/templates/sidebar.php` (potongan menu Kepala Sekolah)
- `app/Config/Routes.php` (grup guru/kelas/ekskul/tahun-ajaran/penugasan-mengajar/kepala-sekolah)
