# INVENTARISASI LENGKAP — Modul Siswa & PSB (Calon Siswa)

Semua path absolut berbasis `C:\xampp\htdocs\webarsipdata-master`.

## 1. SKEMA TABEL AKHIR (hasil akumulasi SEMUA migrasi, urut kronologis)

### 1.1 Migrasi yang memengaruhi `siswa` (urut tanggal)
1. `2026-05-03-000001_CreateSiswaTable.php` — buat tabel awal (kolom `id`, `nama`, `jenis_kelamin`, `nis`, `tempat_lahir`, `tanggal_lahir`, `asal_sekolah`, `alamat_rumah` TEXT, `nama_orang_tua`, `pekerjaan_orang_tua`, `no_handphone`, timestamps).
2. `2026-05-03-000003_CreateKelasTable.php` — buat tabel `kelas`; tambah kolom `kelas_id` INT unsigned nullable ke `siswa` (setelah `nis`).
3. `2026-05-14-000002_CreateRiwayatAkademikAndUpdateSiswa.php` — buat tabel pivot `riwayat_akademik` (siswa_id, tahun_ajaran_id, kelas_id, status enum aktif/naik/lulus, unique(siswa_id,tahun_ajaran_id)); tambah kolom `status` ENUM('aktif','lulus') DEFAULT 'aktif' ke `siswa` (setelah `kelas_id`).
4. `2026-06-29-000001_AddNisnToSiswaTable.php` — tambah `nisn` VARCHAR(20) NULL setelah `nis`.
5. `2026-06-30-000001_SyncSiswaColumnsWithDatabase.php` — idempoten: drop `alamat_rumah`, `nama_orang_tua`, `pekerjaan_orang_tua` (kalau masih ada); tambah `alamat_jalan`(255), `alamat_rt`(5), `alamat_rw`(5), `alamat_kelurahan`(100), `alamat_kecamatan`(100), `nama_ayah`(100), `pekerjaan_ayah`(100), `nama_ibu`(100), `pekerjaan_ibu`(100) — semua VARCHAR NULL. Tabel sudah diubah manual di luar migration sebelumnya.
6. `2026-07-08-000001_RenamePrimaryKeys.php` — rename `id` → `siswa_id` via raw SQL `ALTER TABLE ... CHANGE`.
7. `2026-07-20-000001_AddDokumenColumnsToSiswa.php` — tambah `dokumen_kk`, `dokumen_akta`, `dokumen_kia` (VARCHAR 255 NULL, setelah `no_handphone`).
8. `2026-08-04-000001_AddArchiveLifecycle.php` — raw SQL `ALTER TABLE siswa MODIFY status ENUM('aktif','lulus','pindah','keluar') NOT NULL DEFAULT 'aktif'` (menambah nilai enum `pindah` dan `keluar`); juga tambah `is_active`/`archived_at` ke tabel `kelas` (tidak relevan ke `siswa` langsung). Di `down()`: baris `status='pindah'` di-`UPDATE` jadi `'keluar'` dulu sebelum enum dikembalikan ke `('aktif','lulus')` — catatan: nilai `'pindah'` TIDAK PERNAH dipakai lagi di controller manapun yang dibaca (Siswa::delete/bulkDelete cuma set `'keluar'`), jadi kemungkinan status legacy yang disiapkan tapi tak dipakai UI saat ini.
9. `2026-08-05-000001_AddDokumenIjazahToSiswa.php` — idempoten, tambah `dokumen_ijazah` VARCHAR(255) NULL setelah `dokumen_kia`.
10. `2026-08-08-100000_AddPerformanceIndexesToSiswa.php` — tambah index `idx_siswa_status` pada `status`, `idx_siswa_kelas_id` pada `kelas_id` (guard: skip kalau kolom `status` belum ada — "jangan pernah gagal migrate krn ini").

### 1.2 Skema akhir tabel `siswa` (kolom lengkap, urutan fisik hasil akumulasi)

| Kolom | Tipe | Null | Default | Keterangan |
|---|---|---|---|---|
| `siswa_id` | INT(11) UNSIGNED, AUTO_INCREMENT | NOT NULL | — | PK (rename dari `id`) |
| `nama` | VARCHAR(100) | NOT NULL | — | |
| `jenis_kelamin` | ENUM('L','P') | NOT NULL | — | |
| `nis` | VARCHAR(20) | NOT NULL | — | **UNIQUE** |
| `nisn` | VARCHAR(20) | NULL | — | ditambahkan setelah `nis` fisiknya |
| `kelas_id` | INT(11) UNSIGNED | NULL | NULL | index `idx_siswa_kelas_id`; FK logis ke `kelas.kelas_id` (tidak ada FK constraint eksplisit di migrasi, cuma index) |
| `status` | ENUM('aktif','lulus','pindah','keluar') | NOT NULL | `'aktif'` | index `idx_siswa_status`. `'pindah'` tampaknya sudah tidak dipakai kode aktif |
| `tempat_lahir` | VARCHAR(100) | NULL | — | |
| `tanggal_lahir` | DATE | NULL | — | |
| `asal_sekolah` | VARCHAR(150) | NULL | — | label form: "Asal Sekolah" / di template import: "ASAL TK" |
| `alamat_jalan` | VARCHAR(255) | NULL | — | |
| `alamat_rt` | VARCHAR(5) | NULL | — | |
| `alamat_rw` | VARCHAR(5) | NULL | — | |
| `alamat_kelurahan` | VARCHAR(100) | NULL | — | |
| `alamat_kecamatan` | VARCHAR(100) | NULL | — | |
| `nama_ayah` | VARCHAR(100) | NULL | — | |
| `pekerjaan_ayah` | VARCHAR(100) | NULL | — | |
| `nama_ibu` | VARCHAR(100) | NULL | — | |
| `pekerjaan_ibu` | VARCHAR(100) | NULL | — | |
| `no_handphone` | VARCHAR(20) | NULL (di DB) | — | **DB-level nullable, TAPI wajib diisi di semua jalur aplikasi** (store/update/import baris INSERT) sejak 2026-09-07 |
| `dokumen_kk` | VARCHAR(255) | NULL | — | nama file random hasil upload |
| `dokumen_akta` | VARCHAR(255) | NULL | — | |
| `dokumen_kia` | VARCHAR(255) | NULL | — | |
| `dokumen_ijazah` | VARCHAR(255) | NULL | — | |
| `created_at` | DATETIME | NULL | — | |
| `updated_at` | DATETIME | NULL | — | |

Index: PK(`siswa_id`), UNIQUE(`nis`), KEY `idx_siswa_status`(`status`), KEY `idx_siswa_kelas_id`(`kelas_id`).

`allowedFields` di `SiswaModel`: `nama, jenis_kelamin, nis, nisn, kelas_id, status, tempat_lahir, tanggal_lahir, asal_sekolah, alamat_jalan, alamat_rt, alamat_rw, alamat_kelurahan, alamat_kecamatan, nama_ayah, pekerjaan_ayah, nama_ibu, pekerjaan_ibu, no_handphone, dokumen_kk, dokumen_akta, dokumen_kia, dokumen_ijazah`.

**PENTING**: `SiswaModel::$skipValidation = true` dan `$validationRules = []` — model tidak pernah memvalidasi apa pun sendiri. Semua validasi (regex nama, numeric NIS/NISN/RT/RW, wajib No HP, dsb) HANYA berlaku di controller `Siswa::store()`/`update()` lewat `$this->validate($rules)`. Jalur lain yang menulis langsung ke model — **`Siswa::applyImport()`** dan **`PsbProcessor::terima()`** — **TIDAK melewati validator CI4 ini sama sekali**. Ini harus direplikasi persis: di versi .NET, validasi form manual harus ketat, tapi jalur import/PSB-terima tidak boleh diberi validasi tambahan yang tidak ada di PHP (kecuali cek eksplisit yang memang ada di kode, misalnya cek No HP wajib pada baris INSERT import, dan cek NIS digit-only di PsbProcessor).

### 1.3 Migrasi yang memengaruhi `calon_siswa`
1. `2026-08-05-000002_CreateCalonSiswa.php` — buat tabel baru (guard `if tableExists return`).
2. `2026-09-07-000001_AddTingkatDitujuToCalonSiswa.php` — tambah `tingkat_dituju` VARCHAR(10) NULL setelah `asal_sekolah` (guard idempoten).

### 1.4 Skema akhir tabel `calon_siswa`

| Kolom | Tipe | Null | Default | Keterangan |
|---|---|---|---|---|
| `calon_siswa_id` | INT UNSIGNED, AUTO_INCREMENT | NOT NULL | — | PK |
| `nama` | VARCHAR(100) | NOT NULL | — | |
| `jenis_kelamin` | ENUM('L','P') | NOT NULL | — | |
| `nisn` | VARCHAR(20) | NULL | — | |
| `tempat_lahir` | VARCHAR(100) | NULL | — | |
| `tanggal_lahir` | DATE | NULL | — | |
| `asal_sekolah` | VARCHAR(150) | NULL | — | |
| `tingkat_dituju` | VARCHAR(10) | NULL | — | diisi SAAT pendaftaran; harus salah satu kode tingkat valid (lihat validasi) |
| `alamat_jalan` | VARCHAR(255) | NULL | — | |
| `alamat_rt` | VARCHAR(5) | NULL | — | |
| `alamat_rw` | VARCHAR(5) | NULL | — | |
| `alamat_kelurahan` | VARCHAR(100) | NULL | — | |
| `alamat_kecamatan` | VARCHAR(100) | NULL | — | |
| `nama_ayah` | VARCHAR(100) | NULL | — | |
| `pekerjaan_ayah` | VARCHAR(100) | NULL | — | |
| `nama_ibu` | VARCHAR(100) | NULL | — | |
| `pekerjaan_ibu` | VARCHAR(100) | NULL | — | |
| `no_handphone` | VARCHAR(20) | NULL | — | opsional di form calon siswa (beda dari `siswa` yang wajib) |
| `dokumen_kk` | VARCHAR(255) | NULL | — | |
| `dokumen_akta` | VARCHAR(255) | NULL | — | |
| `dokumen_kia` | VARCHAR(255) | NULL | — | |
| `dokumen_ijazah` | VARCHAR(255) | NULL | — | |
| `catatan` | VARCHAR(255) | NULL | — | diisi saat "tolak" (alasan tidak diterima) |
| `status` | ENUM('menunggu','diterima','ditolak') | NOT NULL | `'menunggu'` | index |
| `siswa_id` | INT UNSIGNED | NULL | — | terisi setelah diterima; baris siswa hasil promosi |
| `created_at` | DATETIME | NULL | — | |
| `updated_at` | DATETIME | NULL | — | |

Index: PK(`calon_siswa_id`), KEY(`status`).

`allowedFields` `CalonSiswaModel`: kolom di atas minus PK/timestamps, ditambah `catatan`, `status`, `siswa_id`. `skipValidation = true` juga — semua validasi murni di controller.

**Catatan arsitektur**: `calon_siswa` SENGAJA tabel terpisah dari `siswa`, BUKAN status baru di `siswa` — karena `siswa` otomatis ter-sync ke Hub API → Absen dan ikut terhitung di laporan/statistik; calon siswa yang belum resmi diterima tidak boleh ikut ke situ. Setelah diterima, baris `calon_siswa` **TETAP disimpan** (bukan dihapus) sebagai jejak riwayat pendaftaran, dan dokumen yang sudah diupload di `calon_siswa` **tidak disalin ulang** ke baris `siswa` baru — baris siswa hanya menunjuk ke nama file yang sama.

### 1.5 Tabel terkait
- `ekskul_siswa`: FK `siswa_id` → `siswa.siswa_id` ON DELETE CASCADE, unique(`ekskul_id`,`siswa_id`).
- `riwayat_akademik`: pivot snapshot kelas/status per tahun ajaran, unique(`siswa_id`,`tahun_ajaran_id`) — tidak disentuh langsung oleh Controller Siswa/CalonSiswa/PsbProcessor.

---

## 2. ROUTE + METHOD CONTROLLER

Semua route di bawah grup dengan filter `['login', 'pendidikan']`.

### Grup `siswa` (prefix `/siswa`)
| Method HTTP | Path | Controller::method |
|---|---|---|
| GET | `/siswa` | `Siswa::index` |
| GET | `/siswa/create` | `Siswa::create` |
| POST | `/siswa/store` | `Siswa::store` |
| GET | `/siswa/print` | `Siswa::print` |
| GET | `/siswa/arsip` | `Siswa::arsip` |
| GET | `/siswa/import` | `Siswa::import` |
| POST | `/siswa/process-import` | `Siswa::previewImport` |
| GET | `/siswa/preview-import` | `Siswa::showPreviewImport` |
| POST | `/siswa/apply-import` | `Siswa::applyImport` |
| GET | `/siswa/download-template` | `Siswa::downloadTemplate` |
| GET | `/siswa/{id}` | `Siswa::detail/{id}` |
| POST | `/siswa/{id}/update` | `Siswa::update/{id}` |
| POST | `/siswa/{id}/delete` | `Siswa::delete/{id}` (ARSIP, bukan hard delete) |
| POST | `/siswa/{id}/delete-dokumen/{field}` | `Siswa::deleteDokumen/{id}/{field}` |
| GET | `/siswa/{id}/dokumen/{field}` | `Siswa::dokumen/{id}/{field}` |
| POST | `/siswa/bulk-delete` | `Siswa::bulkDelete` (ARSIP massal) |

### Grup `calon-siswa` (prefix `/calon-siswa`)
| Method HTTP | Path | Controller::method |
|---|---|---|
| GET | `/calon-siswa` | `CalonSiswa::index` |
| GET | `/calon-siswa/create` | `CalonSiswa::create` |
| POST | `/calon-siswa/store` | `CalonSiswa::store` |
| GET | `/calon-siswa/{id}` | `CalonSiswa::detail/{id}` |
| POST | `/calon-siswa/{id}/update` | `CalonSiswa::update/{id}` |
| POST | `/calon-siswa/{id}/terima` | `CalonSiswa::terima/{id}` |
| POST | `/calon-siswa/{id}/tolak` | `CalonSiswa::tolak/{id}` |
| POST | `/calon-siswa/{id}/delete` | `CalonSiswa::delete/{id}` (HARD delete) |
| POST | `/calon-siswa/{id}/delete-dokumen/{field}` | `CalonSiswa::deleteDokumen/{id}/{field}` |
| GET | `/calon-siswa/{id}/dokumen/{field}` | `CalonSiswa::dokumen/{id}/{field}` |

---

## 3. ALUR LOGIKA DETAIL PER METHOD CONTROLLER

### Konstanta validasi bersama (dari `BaseController`)
```
REGEX_NAMA        = "/^[\p{L}\s'.,\-]+$/u"
REGEX_TEKS_PENDEK = "/^[\p{L}\p{N}\s'.,\/\-]+$/u"
```
Pesan `REGEX_NAMA` (nama, nama_ayah, nama_ibu): "Nama hanya boleh berisi huruf, spasi, titik, koma, strip, dan tanda petik satu." (nama_ayah/nama_ibu → "Nama ayah/ibu hanya boleh...").
Pesan `REGEX_TEKS_PENDEK` per field: "Tempat lahir/Kelurahan/Kecamatan/Pekerjaan ayah/Pekerjaan ibu mengandung karakter yang tidak diizinkan."

### 3.1 `Siswa::dokumen(id, field)`
Endpoint terlindungi login untuk menampilkan dokumen sensitif (file di `WRITEPATH.'uploads/dokumen/'`, fallback `FCPATH.'uploads/dokumen/'`). `$labels`: `dokumen_kk`→"KK", `dokumen_akta`→"Akta", `dokumen_kia`→"KIA", `dokumen_ijazah`→"Ijazah". Field lain/siswa tidak ada/file kosong/file fisik tidak ada → 404 `"Dokumen tidak ditemukan."`. Response header: `Content-Type` asli, `Content-Disposition: inline; filename="{Label}-{nis-tersanitasi}.{ext}"`, `Cache-Control: private, no-store, max-age=0`, `Pragma: no-cache`, `X-Content-Type-Options: nosniff`. NIS disanitasi via `[^A-Za-z0-9_-]+` → `-`.

### 3.2 `Siswa::index()`
`q` (keyword trim), `per_page` (harus salah satu `[50,100,150,200]`, default 50), `page` (min 1).
**Mode pencarian** (keyword tidak kosong): ambil SEMUA siswa `status='aktif'` (join kelas) tanpa limit, filter via `BoyerMoore::search($allData, $keyword, ['nama','nis'])`. Hasil: `totalPages=1`, `page=1`, tanpa paginasi.
**Mode normal**: hitung total aktif/L/P terpisah, `totalPages=ceil(total/perPage)`, clamp page, ambil 1 halaman `limit/offset`, urut `nama ASC`.
`?ajax=1` → render fragmen `siswa/_hasil` saja; else halaman penuh `siswa/index`.

### 3.3 `Siswa::create()`
Form kosong `siswa/create`, `kelasOptions = KelasModel::getDropdown()` (aktif saja, urut tingkat lalu nama).

### 3.4 `Siswa::store()` — aturan validasi PERSIS
| Field | Rules | Pesan error custom |
|---|---|---|
| `nama` | required\|min_length[3]\|max_length[100]\|regex_match[REGEX_NAMA] | "Nama hanya boleh berisi huruf, spasi, titik, koma, strip, dan tanda petik satu." |
| `jenis_kelamin` | required\|in_list[L,P] | — |
| `nis` | required\|max_length[20]\|numeric\|is_unique[siswa.nis] | numeric: "NIS hanya boleh berisi angka." |
| `nisn` | permit_empty\|max_length[20]\|numeric | "NISN hanya boleh berisi angka." |
| `kelas_id` | permit_empty\|integer | — |
| `tempat_lahir` | permit_empty\|max_length[100]\|regex_match[REGEX_TEKS_PENDEK] | "Tempat lahir mengandung karakter yang tidak diizinkan." |
| `tanggal_lahir` | permit_empty\|valid_date[Y-m-d] | — |
| `asal_sekolah` | permit_empty\|max_length[150] | — |
| `alamat_jalan` | permit_empty\|max_length[255] | — |
| `alamat_rt` | permit_empty\|max_length[5]\|numeric | "RT hanya boleh berisi angka." |
| `alamat_rw` | permit_empty\|max_length[5]\|numeric | "RW hanya boleh berisi angka." |
| `alamat_kelurahan` | permit_empty\|max_length[100]\|regex_match[REGEX_TEKS_PENDEK] | "Kelurahan mengandung karakter yang tidak diizinkan." |
| `alamat_kecamatan` | permit_empty\|max_length[100]\|regex_match[REGEX_TEKS_PENDEK] | "Kecamatan mengandung karakter yang tidak diizinkan." |
| `nama_ayah` | permit_empty\|max_length[100]\|regex_match[REGEX_NAMA] | "Nama ayah hanya boleh..." |
| `pekerjaan_ayah` | permit_empty\|max_length[100]\|regex_match[REGEX_TEKS_PENDEK] | "Pekerjaan ayah mengandung karakter yang tidak diizinkan." |
| `nama_ibu` | sama pola nama_ayah | "Nama ibu hanya boleh..." |
| `pekerjaan_ibu` | sama pola pekerjaan_ayah | "Pekerjaan ibu mengandung karakter yang tidak diizinkan." |
| `no_handphone` | **required\|max_length[20]\|numeric** (WAJIB sejak 2026-09-07, dulu permit_empty) | required: "No Handphone wajib diisi - dipakai utk akun Orang Tua di aplikasi HP."; numeric: "No Handphone hanya boleh berisi angka." |
| `dokumen_kk/akta/kia/ijazah` | permit_empty\|max_size[field,2048]\|ext_in[field,pdf,jpg,jpeg,png]\|mime_in[...] | max_size: "Ukuran dokumen {Label} maksimal 2MB."; ext_in: "Dokumen {Label} harus berupa file PDF, JPG, atau PNG."; mime_in: "Tipe/MIME dokumen {Label} tidak valid." |

**Konteks penting**: validasi `no_handphone` diubah wajib 2026-09-07 karena 3 siswa aktif produksi TIDAK PUNYA akun orang tua di Mobile-app karena No HP kosong saat input/import. Berlaku form manual DAN import Excel.

Lolos: bangun data 17 field (dokumen belum termasuk), `kelas_id` kosong→null, `tanggal_lahir` kosong→null. Upload 4 dokumen (`uploadDokumen()`: buat folder `WRITEPATH/uploads/dokumen/` mode 0750 kalau belum ada, nama random via `getRandomName()`). Field dokumen hanya diisi kalau upload berhasil. `insert($data)`. Redirect `/siswa` dengan message "Data siswa berhasil ditambahkan."

### 3.5 `Siswa::detail(id)`
`find($id)` → 404 "Siswa dengan ID $id tidak ditemukan." Render `siswa/detail` dengan `siswa`, `kelasOptions`.

### 3.6 `Siswa::update(id)`
Sama validasi seperti store, `nis` pakai `is_unique[siswa.nis,siswa_id,{id}]`. Gagal → render ulang `siswa/detail`. Lolos: dokumen — file baru diupload → hapus dulu file lama (`hapusDokumen()`) baru set baru; tidak ada file baru → kolom TIDAK disentuh. `update($id, $data)`. Redirect `/siswa` message "Data siswa berhasil diperbarui."

### 3.7 `Siswa::deleteDokumen(id, field)`
Field harus salah satu 4 field dokumen, else redirect back error "Bidang dokumen tidak valid." Siswa tidak ada → 404. Field ada isi → hapus file fisik + set null → "Dokumen berhasil dihapus." Field kosong → "Dokumen tidak ditemukan atau sudah kosong."

### 3.8 `Siswa::delete(id)` — Arsip, BUKAN hapus fisik
404 kalau tidak ada. `update($id, ['status'=>'keluar'])` (identitas & riwayat TIDAK dihapus). Redirect `/siswa` message "Data siswa {nama} dipindahkan ke arsip."
**Edge case penting**: tombol UI & SweetAlert bertuliskan "Hapus Data?"/"akan dihapus permanen", padahal perilaku sebenarnya cuma mengubah status jadi keluar (arsip, reversibel). Ketidaksesuaian ini ADA di PHP asli dan HARUS dipertahankan persis.

### 3.9 `Siswa::bulkDelete()`
`ids` (array) dari POST; kosong/bukan array → error "Tidak ada siswa yang dipilih." Sanitasi cast int, filter >0; hasil kosong → "ID siswa tidak valid." `whereIn('siswa_id',$ids)->set(['status'=>'keluar'])->update()`. Redirect message "$count data siswa dipindahkan ke arsip."

### 3.10 `Siswa::print()`
Semua siswa `status='aktif'` + join kelas, urut nama ASC. Render `siswa/print` (halaman print standalone).

### 3.11 `Siswa::arsip()`
Semua siswa `status != 'aktif'` (termasuk lulus, pindah, keluar), join kelas, urut nama ASC. Render `siswa/arsip`.

### 3.12 `Siswa::import()`
Hapus session `preview_import_siswa`. Render `siswa/import` dengan `kelasOptions`.

### 3.13 `Siswa::previewImport()` — Langkah 1 (klasifikasi, TIDAK sentuh DB)
1. Validasi file ada & valid → error "Upload gagal: {pesan}" / "File tidak ditemukan."
2. Ekstensi `.xlsx`/`.xls` → "Format file harus .xlsx atau .xls"
3. Maks 5MB → "Ukuran file terlalu besar! Maksimal 5MB."
4. `kelas_id` opsional dari POST — kalau diisi (>0) harus kelas `is_active=1`, else "Kelas tujuan tidak valid atau sudah diarsipkan." Berlaku HANYA untuk baris INSERT.
5. Pindahkan ke `WRITEPATH/uploads/{namaRandom}`.
6. `ini_set('memory_limit','512M')`, `ini_set('max_execution_time','300')`.
7. Parse `Shuchkin\SimpleXLSX` — gagal → "Library SimpleXLSX tidak ditemukan..." / "Gagal membaca file Excel: {parseError}".
8. **Header wajib PERSIS** (16 kolom): `Nama, JK, Nis, NISN, TTL, ASAL TK, Jalan, RT, RW, Kel, Kec, Ayah, Ibu, Pekerjaan Ayah, Pekerjaan Ibu, No HP`. Dibandingkan via `normalizeHeaders()`. Tidak cocok → "Urutan kolom tidak sesuai template terbaru. Download ulang template siswa sebelum mengimpor."
9. Loop baris (mulai Excel ke-2): skip kalau 3 kolom pertama (Nama,JK,Nis) semua kosong. Tiap sel cast string dulu. No HP di-strip non-digit.
10. Validasi wajib per baris: NIS kosong → "Baris $rowNum: NIS tidak boleh kosong"; Nama kosong → "Baris $rowNum: Nama tidak boleh kosong"; JK bukan L/P → "Baris $rowNum: Jenis kelamin harus L atau P"; TTL gagal parse → "Baris $rowNum: TTL harus berformat 'Kota, 14 Maret 2018' atau 'Kota, 14-03-2018'"
11. **Cocokkan NIS ke `siswa` TANPA filter status** (NIS tidak pernah didaur ulang).
    - **ADA (KOREKSI)**: `nama` & `jenis_kelamin` SELALU ditulis ulang. Field opsional lain: sel kosong = TIDAK diubah (skip); sel terisi masuk `$data`. TTL sama. **`kelas_id` dan `status` TIDAK PERNAH ikut diubah** lewat baris koreksi. Setiap perubahan dicatat ke `diff[]`.
    - **TIDAK ADA (BARU)**: No HP wajib — kosong → "Baris $rowNum: No HP wajib diisi (dipakai akun Orang Tua di aplikasi HP)" (skip). `$data` diisi semua kolom (kosong→null), `kelas_id`=kelas tujuan upload (atau null), `status='aktif'`.
12. Baris gagal → `$errors[]`, `$ringkasan['error']++`.
13. Hapus file temp.
14. `$preview` kosong → simpan `$errors` ke flashdata `import_errors`, redirect `/siswa/import` error "Tidak ada baris valid untuk diimport."
15. Simpan session `preview_import_siswa` (rows, ringkasan, errors, kelas_id, nama_kelas). Redirect `/siswa/preview-import`.
16. Exception → hapus file temp, redirect back error "Error: {pesan}".

**`parseTtl($ttl)`**: split koma, ambil bagian terakhir sebagai tanggal (`array_pop`), sisanya digabung jadi tempat. Tanggal via `parse_indonesian_date()`. Return `[tempat|null, tanggalY-m-d|null]`.

**`normalizeHeaders($headers)`**: `strtoupper(preg_replace('/\s+/',' ', trim($v)))` — case-insensitive & toleran spasi ganda, TIDAK toleran urutan kolom berbeda.

### 3.14 `Siswa::showPreviewImport()` — Langkah 2
Session kosong → redirect `/siswa/import` error "Belum ada data untuk di-preview. Silakan upload ulang." Render `siswa/preview_import` dengan `preview`, `fieldLabels` (`IMPORT_FIELD_LABELS`).

### 3.15 `Siswa::applyImport()` — Langkah 3 (commit)
Session kosong → redirect error "Belum ada data untuk diterapkan. Silakan upload ulang." `transStart()`: loop rows, `insert` (type='insert') atau `update(siswa_id, data)`. `transComplete()`, hapus session. Gagal → error "Import gagal diterapkan, tidak ada data yang berubah." Sukses → "Berhasil import $inserted siswa baru." + (kalau updated>0) " $updated siswa lama diperbarui." Errors tersisa → flashdata `import_errors`. Redirect `/siswa`.

### 3.16 `Siswa::downloadTemplate()`
`SimpleXLSXGen::fromArray($data)`→`downloadAs('template_import_siswa.xlsx')`+`exit()`. Gagal → fallback CSV (`template_import_siswa.csv`)+`exit()`.

**Isi contoh template**: Header `Nama, JK, Nis, NISN, TTL, ASAL TK, Jalan, RT, RW, Kel, Kec, Ayah, Ibu, Pekerjaan Ayah, Pekerjaan Ibu, No HP`. 2 baris contoh (Budi Santoso L 2024001..., Siti Nurhaliza P 2024002...).

### 3.17 `CalonSiswa::index()`
`status` dari query, default 'menunggu'; harus `['menunggu','diterima','ditolak','semua']` else dipaksa 'menunggu'. `tingkat` opsional filter. Query: `orderBy('tingkat_dituju IS NULL OR tingkat_dituju=""','ASC',false)` lalu `tingkat_dituju ASC` lalu `created_at DESC`. Render dengan `tingkatOptions = KelasModel::getDistinctTingkat()`.

Alasan filter tingkat: laporan "128 kelas 1 + 4 kelas 4 tercampur, susah dicari."

### 3.18 `CalonSiswa::create()`
Render dengan `tingkatOptions`.

### 3.19 `CalonSiswa::rules()` (private, store & update)
Sama pola Siswa untuk nama/jk/tempat_lahir/tanggal_lahir/asal_sekolah/alamat/nama_ayah dll, PLUS `tingkat_dituju`: permit_empty\|max_length[10]\|in_list[{getDistinctTingkat() values}] — harus kode tingkat SUNGGUHAN dipakai kelas aktif. **`no_handphone`: permit_empty (BEDA dari siswa — tetap opsional)**. `dokumenRules()` pola generik sama.

### 3.20 `CalonSiswa::store()`
Validasi gabungan rules+dokumenRules. Lolos: 16 field via `getPost([...])`, `tanggal_lahir` kosong→null. Upload 4 dokumen. `insert($data)`. Redirect "Calon siswa berhasil ditambahkan."

### 3.21 `CalonSiswa::detail(id)`
404 "Calon siswa dengan ID $id tidak ditemukan." `kelasOptions = KelasModel::getDropdownByTingkat($calon['tingkat_dituju'] ?? null)` (fallback SEMUA kelas kalau kosong).

### 3.22 `CalonSiswa::update(id)`
Sama validasi store. Dokumen: file baru → hapus lama dulu. `update($id,$data)`. Redirect ke `/calon-siswa/{id}` (BUKAN index) message "Data calon siswa berhasil diperbarui."

### 3.23 `CalonSiswa::terima(id)` — TERIMA sebagai siswa aktif
1. 404 kalau tidak ada.
2. `status!=='menunggu'` → error "Calon siswa ini sudah diproses sebelumnya."
3. Validasi lokal: `kelas_id`=required\|integer (NIS TIDAK diminta manual sejak 2026-09-07 — auto-generate).
4. `PsbProcessor::terima($id, null, (int)kelas_id)` — NIS selalu null dari web.
5. Gagal → error `$hasil['message']`.
6. Sukses → redirect `/siswa/{siswaId}` dengan `message = $hasil['message']`.

Arsitektur: mutasi sungguhan lewat `PsbProcessor` — SAMA PERSIS yang dipanggil `SyncPush.php::pullKeputusanPsb()`.

### 3.24 `PsbProcessor::terima($calonSiswaId, ?$nis, $kelasId)` — LOGIKA INTI
1. Tidak ada → `{success:false, "Calon siswa dengan ID {id} tidak ditemukan."}`.
2. `status!=='menunggu'` → `{success:false, "Calon siswa ini sudah diproses sebelumnya."}`.
3. `$nis=trim`. Kosong → auto-generate via `generateNis()`. Tidak kosong: harus `ctype_digit` & ≤20 → else `{success:false,"NIS wajib angka, maksimal 20 digit."}`.
4. NIS dipakai siswa lain → `{success:false,"NIS {nis} sudah dipakai siswa lain."}`.
5. Kelas tidak ada → `{success:false,"Kelas tidak ditemukan."}`.
6. Insert `siswa` baru: SEMUA field disalin dari calon (termasuk dokumen — nama file APA ADANYA, tidak disalin fisik), plus nis, kelas_id, status='aktif'.
7. Update `calon_siswa`: status='diterima', siswa_id={hasil insert}.
8. Return `{success:true, "'{nama}' berhasil diterima sebagai siswa aktif.", siswaId}`.

**`generateNis()` format PERSIS**: ambil tahun ajaran aktif; format "YYYY/YYYY" → prefix = 2 digit akhir tahun1 + 2 digit akhir tahun2 (mis. "2026/2027"→"2627"); tidak ada/format salah → prefix "0000". Cari NIS existing `LIKE prefix%` DESC, ambil urutan lanjutan (bukan COUNT): kalau ada & panjang = strlen(prefix)+4 → `urut=(int)substr+1`; else `urut=1`. Loop do-while: `nis=prefix.str_pad(urut,4,'0',STR_PAD_LEFT)`, urut++, ulangi selama NIS dipakai. Hasil 8 digit, mis. `26270001`.

### 3.25 `CalonSiswa::tolak(id)`
404 kalau tidak ada. `catatan` (trim, opsional). `PsbProcessor::tolak($id, $catatan ?: null)`. Gagal → error di detail. Sukses → redirect `/calon-siswa` (index) message.

### 3.26 `PsbProcessor::tolak($calonSiswaId, ?$catatan)`
404 "Calon siswa dengan ID {id} tidak ditemukan." `status!=='menunggu'` → "Calon siswa ini sudah diproses sebelumnya." Lolos: `status='ditolak'`, `catatan=$catatan?:null` (string kosong→NULL). Return "'{nama}' ditandai tidak diterima."

### 3.27 `CalonSiswa::delete(id)` — HARD DELETE (beda dari Siswa::delete!)
404 kalau tidak ada. `status==='diterima'` → TOLAK, error "Calon siswa yang sudah diterima tidak bisa dihapus (sudah jadi data siswa resmi)." Boleh: hapus SEMUA file dokumen fisik, `calonModel->delete($id)` (hard delete beneran). Message "Data calon siswa '{nama}' berhasil dihapus."

### 3.28 `CalonSiswa::dokumen(id,field)`/`deleteDokumen(id,field)`
Pola identik Siswa, nama unduhan "{Label}-Calon-{nama-tersanitasi}.{ext}" (pakai NAMA bukan NIS). Folder cari file cuma 1 lokasi (`uploadPath`, TIDAK ada fallback FCPATH seperti versi Siswa).

---

## 4. HELPER GLOBAL PENTING

### `parse_indonesian_date($date): ?string` (`app/Helpers/date_helper.php`)
- Nama bulan: "14 Maret 2018" → regex `^(\d{1,2})\s+([A-Za-z]+)\s+(\d{4})$`, bulan dipetakan manual via `indonesian_month_number()`, TIDAK memakai `strtotime` untuk kasus ini. Bulan tak dikenal/tanggal invalid → return `null` langsung, TIDAK fallback strtotime.
- Angka: "DD-MM-YYYY" atau "DD/MM/YYYY" (slash→strip) via regex, divalidasi `checkdate`.
- Fallback murni-angka → `strtotime()`, TAPI kalau string mengandung huruf, `strtotime` TIDAK PERNAH dipanggil (return null duluan).

**BUG NYATA (2026-08-26), alasan proteksi ada**: `strtotime("7 Juni 2027")` TIDAK error tapi salah baca — "Jun" dikenali Juni, sisa huruf "i" memutus token sehingga "2027" ditelan jadi JAM 20:27, tahun jatuh ke tahun berjalan → hasil `2026-06-07` (salah 1 tahun) TANPA peringatan. Ditemukan di import Kalender Akademik (4 dari 42 baris salah tahun). Wajib direplikasi persis di .NET — jangan pakai date-parser umum/`DateTime.TryParse` mentah untuk kolom TTL, harus regex eksplisit + peta bulan Indonesia manual + reject-on-ambiguous.

### `indonesian_date($date, $format)`
'short'→"01 Jan 2024", 'medium'(default)→"01 Januari 2024", 'long'→"Senin, 01 Januari 2024", 'full'→"Senin, 01 Januari 2024 08:30 WIB", 'datetime'→"01 Januari 2024, 08:30 WIB". Timezone `Asia/Jakarta`. Input kosong→'-'.

### `BoyerMoore::search($data, $keyword, $fields)`
Substring case-insensitive pada field yang diminta; match salah SATU field. Dipakai HANYA `Siswa::index()` mode pencarian (CalonSiswa tidak punya fitur cari).

---

## 5. VIEW/HALAMAN — Field Form, Tombol, Teks UI Verbatim

### 5.1 `siswa/index.php` + `siswa/_hasil.php`
Tombol aksi: "Print" (`/siswa/print` _blank), "Import Excel", "Tambah Siswa".
Kartu ringkasan: "Total Siswa", "Laki-laki", "Perempuan".
Search placeholder "Cari nama atau NIS...", link "Bersihkan". Dropdown per_page 50/100/150/200 (disembunyikan saat search).
Bulk delete toolbar: "{N} siswa dipilih", "Hapus Terpilih". SweetAlert: "Hapus {N} Siswa?" / "{N} data siswa yang dipilih akan dihapus secara **permanen**." / "Ya, Hapus Semua"/"Batal".
Kolom tabel: checkbox, No, NIS, NISN, Nama(link), JK(badge), Kelas(badge/-), Tempat+Tgl Lahir, Aksi(Detail+hapus).
Empty: search→"Tidak ada siswa yang cocok dengan pencarian \"{keyword}\"."; else→"Belum ada data siswa. Tambah sekarang."
Hasil pencarian text: "Hasil pencarian untuk: {keyword} — ditemukan {totalSiswa} siswa."
Pagination: "Menampilkan {from} sampai {to} dari {totalSiswa} data".
JS: debounce 300ms fetch `ajax=1`, suntik ke `#hasilSiswa`, `history.replaceState`. Toolbar bulk delete disembunyikan via `setProperty('display','none','important')` (bukan langsung style.display) karena class Bootstrap `d-flex !important`.

### 5.2 `siswa/create.php`
Kartu "Identitas Siswa": Nama Lengkap*, Jenis Kelamin* (select L/P), NIS*, NISN, Kelas(select), Tempat Lahir, Tanggal Lahir, Asal Sekolah, Alamat Rumah(`alamat_jalan`), RT, RW, Kelurahan, Kecamatan.
Kartu "Data Orang Tua/Wali": Nama Ayah, Pekerjaan Ayah, Nama Ibu, Pekerjaan Ibu, No. Handphone (placeholder "Contoh: 08123456789" — **HTML tidak ditandai required walau validasi server WAJIB** — ketidaksesuaian UI vs validasi).
Kartu "Dokumen Siswa": info "Format: PDF, JPG, JPEG, PNG — Maks. 2MB per file." Field: KK, Akta Kelahiran, KIA, Ijazah.
Tombol: "Simpan Data Siswa", "Batal".

### 5.3 `siswa/detail.php`
Header: "{nama} • NIS: {nis} • NISN: {nisn|-}". Form sama field create + old(). NISN hint: "NISN adalah nomor identitas siswa yang bersifat nasional dan unik". Dokumen: preview thumbnail/icon, tombol Lihat+hapus, "Upload file baru untuk mengganti:". Tombol "Simpan Perubahan"/"Batal".
Kartu Ringkasan: Nama, NIS, NISN, JK("Laki-laki"/"Perempuan"), Ditambahkan. Tombol "Hapus Data Ini" (danger) → form ke `/siswa/{id}/delete`, SweetAlert global.

### 5.4 `siswa/arsip.php`
Partial `arsip/_tabs`. Judul "Arsip Siswa", subteks "Siswa yang sudah lulus/keluar. Data tidak dihapus, cuma tidak lagi tampil di Data Siswa aktif."
Kolom: No, NIS, Nama, JK, Kelas Terakhir, Status (badge — "Lulus" kalau status==='lulus', SELALU "Keluar" selain itu, termasuk 'pindah'), Aksi (Detail saja, TIDAK ADA hapus/restore).
Empty: "Belum ada siswa yang diarsipkan."

### 5.5 `siswa/import.php`
Instruksi ordered list: 1.Download template 2.Isi kolom (Nama wajib, JK L/P wajib, NIS wajib unik, NISN opsional, TTL "Kota, Tanggal Bulan Tahun" contoh "Bekasi, 14 Maret 2018" atau angka "Jakarta, 15-05-2018", ASAL TK opsional, Alamat opsional, Nama Orang Tua opsional, Pekerjaan Orang Tua opsional, No HP wajib) 3.Simpan .xlsx/.xls 4.Upload.
Catatan Penting: "Kelas tujuan dipilih satu kali... berlaku untuk siswa BARU saja."; "File ini bisa berisi campuran... NIS SUDAH ADA... otomatis dianggap koreksi (kolom yang dikosongkan tidak akan diubah)... Kelas siswa lama TIDAK ikut berpindah..."; "...halaman Preview... konfirmasi dulu baru diterapkan."; "No. HP yang diisi akan disinkronkan sebagai nomor orang tua penerima notifikasi presensi."
Tombol "Download Template Excel", field Kelas Tujuan (hint "Semua siswa dalam file akan langsung dimasukkan ke kelas ini."), file input required, tombol "Upload & Import Data"→spinner "Sedang mengimport...", "Kembali ke Data Siswa".
JS validasi ukuran client-side >5MB → alert native, reset input.

### 5.6 `siswa/preview_import.php`
Badge: "{insert} Baru"(success), "{update} Diperbarui"(warning), "{error} Error"(danger, >0 saja).
Teks: "Periksa dulu hasil klasifikasi... Baris Baru akan ditambahkan... Baris Diperbarui akan MENGOREKSI... kolom yang tidak diisi... TIDAK akan diubah, kelas siswa juga tidak ikut berpindah."
Tabel: Baris, NIS, Nama, Status(badge), Perubahan (insert:"Siswa baru, seluruh data pada file akan disimpan."; update tanpa diff:"Tidak ada perubahan..."; update dengan diff: list "{Label}: ~~{old|-}~~ → {new|-}").
Tombol "Terapkan Sekarang ({insert+update} baris)"/"Batal".
`IMPORT_FIELD_LABELS`: nama→Nama, jenis_kelamin→Jenis Kelamin, nisn→NISN, tempat_lahir→Tempat Lahir, tanggal_lahir→Tanggal Lahir, asal_sekolah→Asal TK, alamat_jalan→Jalan, alamat_rt→RT, alamat_rw→RW, alamat_kelurahan→Kelurahan, alamat_kecamatan→Kecamatan, nama_ayah→Nama Ayah, pekerjaan_ayah→Pekerjaan Ayah, nama_ibu→Nama Ibu, pekerjaan_ibu→Pekerjaan Ibu, no_handphone→No HP.

### 5.7 `siswa/print.php`
Standalone. Header "SDIT AL IKHLAS 86"/"Daftar Data Siswa"/"Dicetak pada: {datetime} | Total: {n} siswa". Tombol "🖨 Cetak Sekarang"/"✕ Tutup".
Kolom: No, NIS, Nama Lengkap, JK, Kelas, Tempat/Tgl Lahir, Asal Sekolah, Alamat(gabungan), Nama Ayah, Nama Ibu, No. HP. Footer Total. Empty: "Tidak ada data siswa."

### 5.8 `calon_siswa/index.php`
Tombol "Tambah Calon Siswa". Filter tingkat (auto-submit): "Semua Tingkat"+opsi. Filter status button group: Menunggu|Diterima|Ditolak|Semua.
Kolom: No, Nama(link), JK, Tingkat Dituju(badge/"belum diisi" italic), Asal Sekolah, No.HP, Status(badge: menunggu=warning/"Menunggu", diterima=success/"Diterima", ditolak=secondary/"Ditolak"), Didaftarkan, Aksi(Detail saja).
Empty: "Belum ada calon siswa{ dengan status ini}. Tambah sekarang."

### 5.9 `calon_siswa/create.php`
Alert info: "NIS dibuat otomatis & kelas... baru dipilih saat calon siswa diterima jadi siswa aktif - Tingkat/Kelas yang Dituju di bawah cukup perkiraan awal..."
Field identik siswa/create untuk identitas+ortu, PLUS "Tingkat/Kelas yang Dituju" (select, "-- Belum tahu / lainnya --"+tingkatOptions, hint "Mis. siswa pindahan langsung ke Kelas 4, bukan Kelas 1.").
TIDAK ada field NIS/Kelas spesifik. No.Handphone keterangan "(penting untuk dihubungi)", TIDAK required.
Kartu dokumen "Dokumen (opsional, bisa menyusul)".
Tombol "Simpan Calon Siswa"/"Batal".

### 5.10 `calon_siswa/detail.php`
Header: "{nama} • Status: {Ucfirst(status)}". Form edit sama field create. Dokumen PDF cuma tampil icon+teks "PDF" (beda dari siswa/detail).
Kartu Ringkasan: Nama, Status, Didaftarkan, Catatan(kalau ada).
Kartu "Terima sebagai Siswa" (HANYA status='menunggu'): Alert "NIS dibuat otomatis (2026-09-07)..." Field "Kelas *" (required, dibatasi tingkat_dituju). Hint dinamis: kosong→warning "Tingkat tujuan belum diisi..."; terisi→"Daftar dibatasi ke kelas tingkat \"{tingkat_dituju}\"...". Tombol "Terima & Pindahkan ke Kelas" (TANPA SweetAlert konfirmasi).
Kartu tolak: "Catatan (opsional)" placeholder "Alasan tidak diterima", tombol "Tandai Tidak Diterima" (SweetAlert "Tandai Tidak Diterima?").
Kondisi diterima+siswa_id: alert success "Sudah diterima sebagai siswa aktif. Lihat data siswa".
Kartu hapus (status!=='diterima'): "Hapus Permanen" (SweetAlert "Hapus Permanen?"/"Data calon siswa {nama} akan dihapus permanen.").

### 5.11 SweetAlert global (`templates/index.php`, delegasi event submit ke `document`)
`form-delete-siswa`: "Hapus Data?"/"Data {nama} akan dihapus permanen."/warning/"Ya, Hapus"/"Batal"/#d33.
`form-delete-dokumen`: "Hapus Dokumen?"/"Dokumen ini akan dihapus secara permanen."
Delegasi ke `document` SENGAJA — baris tabel Siswa diganti via AJAX (pencarian/pagination); listener ke elemen lama tidak akan pernah dapat proteksi.

---

## 6. PERILAKU HALUS / EDGE CASE YANG MUDAH TERLEWAT

1. **BUG NYATA `strtotime` nama bulan Indonesia (2026-08-26)** — lihat §4. Wajib direplikasi: parser tanggal custom (regex+peta bulan Indonesia), bukan `DateTime.Parse`/`TryParseExact` culture umum.
2. **No HP wajib untuk `siswa` (bukan `calon_siswa`), sejak 2026-09-07** — SEMUA jalur yang membuat siswa AKTIF (form manual store/update, baris INSERT import). TIDAK berlaku baris UPDATE import (kosong=tidak diubah) dan TIDAK berlaku `calon_siswa`/`PsbProcessor::terima()` — kalau calon terdaftar tanpa No HP, siswa hasil promosi WARISI kosong tanpa blocker tambahan (celah asli, replikasi apa adanya).
3. **NIS pencocokan import TANPA filter status** — cocok ke SEMUA baris siswa termasuk arsip.
4. **Sel kosong pada baris KOREKSI = "jangan diubah"**, kecuali `nama`/`jenis_kelamin` yang SELALU ditulis ulang.
5. **`kelas_id`/`status` SENGAJA tidak pernah berubah lewat baris koreksi import**.
6. **Validasi CI4 TIDAK berlaku di `applyImport()`/`PsbProcessor::terima()`** — model `skipValidation=true`. Field lain (nama_ayah dst) dari Excel TIDAK divalidasi format karakternya. Jangan tambahkan validasi ekstra di jalur ini saat porting.
7. **`Siswa::delete()`/`bulkDelete()` = ARSIP, BUKAN hard delete** walau teks bilang "Hapus Data?"/"permanen" — pertahankan ketidaksesuaian ini.
8. **`CalonSiswa::delete()` = HARD DELETE beneran**, diblokir kalau status='diterima'.
9. **Dokumen calon siswa TIDAK disalin ulang saat "Terima"** — baris siswa baru menunjuk NAMA FILE SAMA. `calon_siswa` TIDAK PERNAH dihapus setelah diterima.
10. **`tingkat_dituju` ditambahkan 2026-09-07** diisi SAAT PENDAFTARAN. Nullable, value harus kode tingkat SUNGGUHAN dipakai.
11. **`getDropdownByTingkat()` fallback SEMUA kelas** kalau `tingkat_dituju` kosong/null.
12. **Urutan sort `CalonSiswa::index()`**: tingkat dulu (NULL di akhir, raw SQL expression param3=false), lalu created_at DESC. Harus direplikasi sebagai raw SQL/CASE WHEN, bukan ORDER BY kolom biasa.
13. **Mode pencarian `Siswa::index()` mengabaikan per_page/pagination sepenuhnya**; hitungan totalL/totalP berubah makna (normal=SEMUA aktif; pencarian=dari HASIL PENCARIAN saja).
14. **`is_unique[siswa.nis,siswa_id,{id}]` di update** exclude baris sendiri.
15. **Nama unduhan dokumen**: siswa pakai NIS, calon siswa pakai NAMA (belum punya NIS). Sanitasi sama.
16. **Fallback lokasi file dokumen SISWA ada 2 folder** (WRITEPATH+FCPATH), **CALON SISWA hanya 1 folder** — asimetri nyata di kode, replikasi sesuai aslinya per modul.
17. **Bulk delete/checklist hanya di Siswa**, tidak ada di Calon Siswa.
18. **Placeholder hint tingkat_dituju** hanya di create.php, tidak di detail.php.
19. **`ext_in`+`mime_in` dokumen** 2 lapis validasi (ekstensi DAN mime asli).
20. **Filter instalasi**: seluruh route Siswa & CalonSiswa dibungkus `['login','pendidikan']` — hanya aktif tipe "pendidikan".

---

## RINGKASAN FILE YANG DIBACA TUNTAS
- `app/Controllers/Siswa.php`, `CalonSiswa.php`
- `app/Models/SiswaModel.php`, `CalonSiswaModel.php`
- `app/Libraries/PsbProcessor.php`, `BoyerMoore.php`
- `app/Helpers/date_helper.php`
- `app/Views/siswa/*.php` (index, _hasil, create, detail, arsip, import, preview_import, print)
- `app/Views/calon_siswa/index.php, create.php, detail.php`
- `app/Views/arsip/_tabs.php`
- `app/Views/templates/index.php` (SweetAlert global)
- `app/Config/Routes.php` (grup siswa & calon-siswa)
- `app/Controllers/BaseController.php` (REGEX_NAMA, REGEX_TEKS_PENDEK)
- Migrasi kronologis lengkap (lihat §1.1/1.3)
- Terkait: `2026-08-30-000001_CreateEkskulTables.php`, `KelasModel.php` (getDropdown, getDropdownByTingkat, getDistinctTingkat)
