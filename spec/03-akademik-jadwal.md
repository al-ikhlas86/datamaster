# Inventarisasi Lengkap — Modul Kurikulum, Jadwal Pelajaran, Kalender Akademik, dan Akademik

Semua path absolut di `C:\xampp\htdocs\webarsipdata-master`.

## 0. Peta Modul & Perbedaan Cakupan

| Controller | Route prefix | Isi |
|---|---|---|
| `app/Controllers/Kurikulum.php` | `kurikulum` | Master data kurikulum: 3 tab (Mata Pelajaran, Struktur Kurikulum/Alokasi JP, Jam Belajar) + endpoint Master Tingkat (dipanggil dari menu Kelas) |
| `app/Controllers/JadwalPelajaran.php` | `jadwal-pelajaran` | Pengisian grid jadwal per kelas (kode "35K"), salin jadwal, piket, cetak, import Excel, jadwal per guru |
| `app/Controllers/KalenderAkademik.php` | `kalender-akademik` | Kalender libur & agenda kegiatan sekolah (CRUD + import Excel dengan parser tanggal gaya Indonesia) |
| `app/Controllers/Akademik.php` | `akademik` | **BEDA TOTAL cakupannya** — proses kenaikan kelas & kelulusan, arsip historis siswa (riwayat_akademik), rekap lulusan. Tidak menyentuh mata_pelajaran/jadwal/kalender. |

---

## 1. SKEMA TABEL AKHIR

### 1.1 Urutan migrasi kronologis relevan
1. `2026-05-14-000002_CreateRiwayatAkademikAndUpdateSiswa.php` — `riwayat_akademik` (PK awal `id`) + `siswa.status` enum(aktif,lulus) default aktif.
2. `2026-07-08-000001_RenamePrimaryKeys.php` — rename PK: `riwayat_akademik.id`→`riwayat_akademik_id` (juga siswa/kelas/tahun_ajaran).
3. `2026-08-19-000001_CreateMataPelajaranTable.php` — `mata_pelajaran` (awal cuma `nama` unik).
4. `2026-08-19-000002_CreateJadwalPelajaranTable.php` — `jadwal_pelajaran` versi LAMA (hari, jam_mulai, jam_selesai bebas ketik, guru_id NOT NULL).
5. `2026-08-20-000001_CreateTingkatTable.php` — `tingkat` + seed 9 baris (Playgroup, TK-A, TK-B, Kelas 1-6) + baris tambahan untuk nilai `kelas.tingkat` tak dikenal.
6. `2026-08-20-000002_AddKurikulumFieldsToMataPelajaran.php` — tambah `kode`,`kelompok`,`urutan` + unique `uniq_mapel_kode`.
7. `2026-08-20-000004_CreateKurikulumAlokasiTable.php` — `kurikulum_alokasi`.
8. `2026-08-20-000005_CreateJamPelajaranTable.php` — `jam_pelajaran` + `jam_pelajaran_tingkat`.
9. `2026-08-20-000006_RestructureJadwalPelajaran.php` — **rombak besar**: kolom hari/jam_mulai/jam_selesai DIHAPUS, diganti `jam_pelajaran_id` (FK CASCADE), `guru_id` nullable, unique baru `(tahun_ajaran_id, semester, kelas_id, jam_pelajaran_id)`. Migrasi memindahkan data lama otomatis.
10. `2026-08-20-000007_CreateJadwalPiketTable.php` — `jadwal_piket`.
11. `2026-08-20-000008_CreateKalenderAkademikTable.php` — `kalender_akademik`.

### 1.2 `mata_pelajaran`
| Kolom | Tipe | Constraint |
|---|---|---|
| mata_pelajaran_id | INT(11) UNSIGNED | PK, AUTO_INCREMENT |
| nama | VARCHAR(100) | NOT NULL, UNIQUE |
| kode | VARCHAR(10) | NULL, UNIQUE (`uniq_mapel_kode`) — NULL boleh berulang |
| kelompok | VARCHAR(60) | NULL |
| urutan | INT(11) | DEFAULT 0 |
| created_at, updated_at | DATETIME | NULL |

### 1.3 `tingkat`
| Kolom | Tipe | Constraint |
|---|---|---|
| tingkat_id | INT(11) UNSIGNED | PK, AUTO_INCREMENT |
| kode | VARCHAR(20) | UNIQUE — identitas tetap, dipakai `kelas.tingkat` & `kurikulum_alokasi.tingkat_kode` |
| nama | VARCHAR(50) | label tampilan, bebas diubah |
| urutan | INT(11) | DEFAULT 0 |
| is_active | TINYINT(1) | DEFAULT 1 |
| created_at, updated_at | DATETIME | NULL |

Seed: Playgroup(1), TK-A→"TK A"(2), TK-B→"TK B"(3), "1".."6"→"Kelas 1 SD".."Kelas 6 SD"(11-16).

### 1.4 `kurikulum_alokasi`
| Kolom | Tipe | Constraint |
|---|---|---|
| kurikulum_alokasi_id | INT(11) UNSIGNED | PK |
| tahun_ajaran_id | INT(11) UNSIGNED | FK→tahun_ajaran CASCADE |
| tingkat_kode | VARCHAR(20) | — |
| mata_pelajaran_id | INT(11) UNSIGNED | FK→mata_pelajaran CASCADE |
| jp_per_minggu | INT(11) | DEFAULT 0 |
| created_at, updated_at | DATETIME | NULL |

UNIQUE `(tahun_ajaran_id, tingkat_kode, mata_pelajaran_id)`. **Baris TIDAK ADA = mapel tidak diajarkan** — nilai 0 sengaja tidak disimpan (baris dihapus).

### 1.5 `jam_pelajaran`
| Kolom | Tipe | Constraint |
|---|---|---|
| jam_pelajaran_id | INT(11) UNSIGNED | PK |
| tahun_ajaran_id | INT(11) UNSIGNED | FK CASCADE |
| hari | ENUM('senin'..'minggu') | — |
| jam_ke | INT(11) | — |
| jam_mulai, jam_selesai | TIME | — |
| jenis | ENUM('pelajaran','kegiatan') | DEFAULT 'pelajaran' |
| label | VARCHAR(100) | NULL — diisi kalau jenis='kegiatan' |
| created_at, updated_at | DATETIME | NULL |

UNIQUE `(tahun_ajaran_id, hari, jam_ke)`. **Hari tanpa baris = otomatis libur.**

### 1.6 `jam_pelajaran_tingkat`
| Kolom | Tipe | Constraint |
|---|---|---|
| jam_pelajaran_tingkat_id | INT(11) UNSIGNED | PK |
| jam_pelajaran_id | INT(11) UNSIGNED | FK CASCADE |
| tingkat_kode | VARCHAR(20) | — |

UNIQUE `(jam_pelajaran_id, tingkat_kode)`. **KOSONG (tidak ada baris) = berlaku SEMUA tingkat.**

### 1.7 `jadwal_pelajaran` (SETELAH restrukturisasi)
| Kolom | Tipe | Constraint |
|---|---|---|
| jadwal_pelajaran_id | INT(11) UNSIGNED | PK |
| tahun_ajaran_id | INT(11) UNSIGNED | FK CASCADE |
| semester | ENUM('ganjil','genap') | NOT NULL |
| kelas_id | INT(11) UNSIGNED | FK CASCADE |
| jam_pelajaran_id | INT(11) UNSIGNED | NOT NULL, FK CASCADE |
| mata_pelajaran_id | INT(11) UNSIGNED | NOT NULL, FK RESTRICT |
| guru_id | INT(11) UNSIGNED | **NULLABLE** (mapel tanpa guru tetap), FK RESTRICT |
| created_at, updated_at | DATETIME | NULL |

UNIQUE `uniq_jadwal_slot(tahun_ajaran_id,semester,kelas_id,jam_pelajaran_id)`. Index `idx_guru_jam(guru_id,jam_pelajaran_id,semester)`.

### 1.8 `jadwal_piket`
| Kolom | Tipe | Constraint |
|---|---|---|
| jadwal_piket_id | INT(11) UNSIGNED | PK |
| tahun_ajaran_id | INT(11) UNSIGNED | FK CASCADE |
| semester | ENUM('ganjil','genap') | — |
| hari | ENUM(7 hari) | — |
| guru_id | INT(11) UNSIGNED | FK CASCADE |
| created_at, updated_at | DATETIME | NULL |

UNIQUE `(tahun_ajaran_id, semester, hari, guru_id)`. Jumlah guru piket per hari bebas.

### 1.9 `kalender_akademik`
| Kolom | Tipe | Constraint |
|---|---|---|
| kalender_akademik_id | INT(11) UNSIGNED | PK |
| tahun_ajaran_id | INT(11) UNSIGNED | FK CASCADE |
| judul | VARCHAR(200) | NOT NULL |
| kategori | VARCHAR(60) | NULL — teks BEBAS bukan enum |
| warna | VARCHAR(20) | NULL — hex |
| tanggal_mulai | DATE | NOT NULL |
| tanggal_selesai | DATE | NULL — kosong = 1 hari |
| waktu | VARCHAR(60) | NULL |
| sasaran | VARCHAR(120) | NULL |
| is_libur | TINYINT(1) | DEFAULT 0 |
| keterangan | TEXT | NULL |
| created_at, updated_at | DATETIME | NULL |

Index `(tahun_ajaran_id, tanggal_mulai)`.

### 1.10 `riwayat_akademik`
| Kolom | Tipe | Constraint |
|---|---|---|
| riwayat_akademik_id | INT(11) UNSIGNED | PK |
| siswa_id | INT(11) UNSIGNED | NOT NULL, tanpa FK eksplisit |
| tahun_ajaran_id | INT(11) UNSIGNED | NOT NULL, tanpa FK eksplisit |
| kelas_id | INT(11) UNSIGNED | NOT NULL, tanpa FK eksplisit — "Kelas siswa pada tahun ajaran tersebut" |
| status | ENUM('aktif','naik','lulus') | DEFAULT 'aktif' |
| created_at, updated_at | DATETIME | NULL |

UNIQUE `uq_siswa_tahun(siswa_id, tahun_ajaran_id)`.

`siswa` juga dapat kolom `status` ENUM('aktif','lulus') DEFAULT 'aktif' (setelah `kelas_id`).

### 1.11 `tahun_ajaran` (konteks)
`tahun_ajaran_id` PK, `nama` VARCHAR (model: required min4 max20; format YYYY/YYYY dipaksa di CONTROLLER `TahunAjaran::store()` via `regex_match[/^\d{4}\/\d{4}$/]`, BUKAN di model), `is_active` TINYINT.

---

## 2. ROUTES

Semua route filter `['login','pendidikan']`.

### Kurikulum (prefix `kurikulum`)
GET `kurikulum`→index; POST `kurikulum/mata-pelajaran/store`→storeMapel; POST `.../update`→updateMapelBatch; POST `.../{id}/delete`→deleteMapel; GET `.../import`→importMapel; GET `.../template`→downloadTemplateMapel; POST `.../process-import`→processImportMapel; POST `kurikulum/alokasi/simpan`→simpanAlokasi; POST `.../salin`→salinAlokasi; GET `.../import`→importAlokasi; GET `.../template`→downloadTemplateAlokasi; POST `.../process-import`→processImportAlokasi; POST `kurikulum/jam/store`→storeJam; POST `.../update`→updateJamBatch; POST `.../{id}/delete`→deleteJam; POST `.../salin`→salinJam; GET `.../import`→importJam; GET `.../template`→downloadTemplateJam; POST `.../process-import`→processImportJam; POST `kurikulum/tingkat/store`→storeTingkat; POST `.../update`→updateTingkatBatch; POST `.../{id}/delete`→deleteTingkat.

### Jadwal Pelajaran (prefix `jadwal-pelajaran`)
GET index; POST `simpan`→simpanGrid; POST `salin`; POST `piket`→simpanPiket; GET `cetak`; GET `guru`→perGuru; GET `import`; GET `download-template`; POST `process-import`.

### Kalender Akademik (prefix `kalender-akademik`)
GET index; POST `store`; POST `{id}/update`; POST `{id}/delete`; GET `import`; GET `download-template`; POST `process-import`.

### Akademik (grup `akademik`)
GET `akademik/`→index; POST `proses-naik-kelas`; POST `proses-kelulusan`; GET `arsip`; POST `arsip/bulk-delete`; GET `rekap-lulusan`; POST `rekap-lulusan/bulk-delete`.

---

## 3. MODUL KURIKULUM — detail per method (`Kurikulum.php`)

Tab "Master Tingkat" SUDAH TIDAK ditampilkan di halaman ini sejak 2026-08-27 — endpoint `storeTingkat/updateTingkatBatch/deleteTingkat` MASIH aktif, dipanggil dari menu **Kelola Kelas**.

### `resolveTahunAjaranId()` (private)
GET `tahun_ajaran_id` kalau ada&truthy, else `TahunAjaranModel::getActive()`, else 0.

### `index()`
`tahunAjaranId`, `tab` (default 'mapel', juga 'jam'/'alokasi'). Data: `tahunAjaranList`(DESC), `mapelList`(getTerurut), `mapelPerKelompok`, `kelompokTerpakai`, `tingkatList`(getAktif), `matriks`(getMatriks), `jamPerHari`(getPerHari), `hariList`=JamPelajaranModel::HARI.

### `tujuanTingkat()`/`back()` (private)
`back($tab,$ta)`→redirect `kurikulum?tab={tab}&tahun_ajaran_id={id}`. `tujuanTingkat()`: POST `kembali_ke==='kelas'` (SATU-SATUNYA nilai diterima, whitelist anti-open-redirect) → redirect `base_url('kelas')`, else `back()`.

### TAB 1 — Mata Pelajaran
**`storeMapel()`**: `tahun_ajaran_id`, `kode`(trim,kosong→null,isi→uppercase), `kelompok`(trim,kosong→null), `urutan`(int), `nama`(trim). Insert via model (validasi lihat §5.1). Gagal→error gabungan model. Sukses→"Mata pelajaran \"{nama}\" ditambahkan."

**`updateMapelBatch()`**: batch simpan semua baris. **WAJIB** kirim `mata_pelajaran_id=(int)$id` di `$data` sebelum update — placeholder `{mata_pelajaran_id}` di rule `is_unique` HARUS terisi, kalau tidak `is_unique` mengecek SELURUH tabel termasuk baris itu sendiri → selalu "sudah ada" walau nama tidak diubah. Ini akar bug update mapel selalu gagal. `kode`/`kelompok` trim, kosong→null, kode uppercase. Gagal→`$gagal[]="{nama}: {pesan}"`. Flashdata `import_errors` kalau ada gagal. Message: "{ok} mata pelajaran diperbarui." (selalu sukses redirect walau sebagian gagal).

**`deleteMapel(id)`**: tidak ada→"Mata pelajaran tidak ditemukan." Delete dalam try/catch `DatabaseException` (FK RESTRICT dari jadwal_pelajaran)→"Tidak bisa menghapus \"{nama}\" - masih dipakai di Jadwal Pelajaran. Hapus jadwalnya dulu." Sukses→"Mata pelajaran \"{nama}\" dihapus."

### TAB 2 — Alokasi JP
**`simpanAlokasi()`**: tanpa `tahun_ajaran_id`→"Tahun ajaran belum dipilih." Loop `alokasi[tingkat][mapel]=jp`, panggil `simpanSel()` per sel (≤0=hapus, >0=insert/update). Message: "Alokasi kurikulum disimpan ({n} sel diperiksa)."

**`salinAlokasi()`**: `tahunAjaranId`&`sumberId` wajib beda→"Pilih tahun ajaran sumber yang berbeda." `salinDari()` — baris sudah ada TIDAK ditimpa. Message: "{n} baris alokasi disalin. Baris yang sudah ada tidak ditimpa."

### TAB 3 — Jam Belajar
**`storeJam()`** (AJAX-aware):
1. `tahun_ajaran_id, hari, jenis(default'pelajaran' kecuali literal 'kegiatan'), jam_ke(int), jam_mulai, jam_selesai, label`(hanya kegiatan: trim atau fallback 'Kegiatan', else null).
2. Closure `$gagal($pesan)`: AJAX→422 JSON `{success:false,message}`; else redirect+flash error.
3. Wajib: `tahun_ajaran_id` truthy, `hari` valid HARI, `jam_mulai`/`jam_selesai` tidak kosong → "Hari, jam mulai, dan jam selesai wajib diisi."
4. `jam_mulai>=jam_selesai`→"Jam selesai harus setelah jam mulai."
5. `jam_ke<=0`→dihitung MAX(jam_ke) untuk (ta,hari)+1 (default 1).
6. **Bentrok NOMOR jam**: COUNT where (ta,hari,jam_ke) >0 → "Jam ke-{jamKe} pada hari {hari} sudah ada."
7. **TUMPANG TINDIH WAKTU**: `WHERE ta=$ta AND hari=$hari AND jam_mulai<$jamSelesai AND jam_selesai>$jamMulai` (rumus overlap standar `A.mulai<B.selesai AND A.selesai>B.mulai`, berlaku walau nomor jam beda). Ada hasil: `labelBentrok`=label kegiatan atau literal 'jam pelajaran'; `waktuBentrok`=substr HH:MM-HH:MM. Pesan: "Waktu {mulai}-{selesai} bentrok dengan \"{labelBentrok}\" ({waktuBentrok}) pada hari {hari}."
8. Insert (tanpa validasi model).
9. AJAX sukses: JSON `{success:true, jam:{...}}`. Non-AJAX: redirect message "Jam ke-{jamKe} hari {hari} ditambahkan."

Komentar penting: DUA aturan bentrok dipisah jadi fungsi (`cekBentrokNomorJam`/`cekTumpangTindihJam`) 2026-09-07 supaya dipakai ulang `processImportJam()` — WAJIB selalu identik di jalur manual & impor.

**`updateJamBatch()`**: per baris `{jam_mulai,jam_selesai,jenis,label}` — kosong atau `jam_mulai>=jam_selesai`→gagal "Jam ke-{jam_ke} {hari}: jam selesai harus setelah jam mulai." (baris dilewati). `jenis`='kegiatan' eksplisit else 'pelajaran'. **CATATAN KRUSIAL**: batch update ini **TIDAK mengecek ulang bentrok nomor/tumpang tindih** — hanya `jam_mulai<jam_selesai`. INKONSISTENSI NYATA vs storeJam/processImportJam — WAJIB direplikasi apa adanya (bukan diperbaiki) kecuali diminta eksplisit. Message: "{ok} baris jam belajar diperbarui."

**`deleteJam(id)`**: tidak ada→"Jam pelajaran tidak ditemukan." FK CASCADE ke jadwal_pelajaran — hitung dulu berapa baris terhapus. "Jam ke-{jam_ke} hari {hari} dihapus." + (kalau ada) " {n} isian jadwal pada jam tersebut ikut terhapus."

**`salinJam()`**: sama validasi salinAlokasi. Message: "{n} baris jam belajar disalin."

### IMPORT — Mata Pelajaran
`importMapel()` GET→view jenis='mapel'.
`downloadTemplateMapel()`: XLSX sheet "Mata Pelajaran", header `['Nama','Kode','Kelompok','Urutan']`, 3 baris contoh (PAI/A/Kelompok A (Umum)/1, dst). File `template_mata_pelajaran.xlsx`.
`processImportMapel()`: baca via `bacaExcelImport()`. Loop: kolom 0=Nama,1=Kode,2=Kelompok,3=Urutan. Baris kosong (nama&kode kosong)→skip diam-diam. Nama kosong(kode ada)→"Baris {no}: nama mata pelajaran wajib diisi." Insert; gagal validasi→"Baris {no} ({nama}): {pesan model}". Message: "Import selesai: {masuk} mata pelajaran masuk, {dilewati} dilewati."

### IMPORT — Jam Belajar
`importJam()` GET→jenis='jam'.
`downloadTemplateJam()`: sheet "Jam Belajar" (Hari, Jam Ke(kosong=otomatis), Jenis, Jam Mulai, Jam Selesai, Label) + sheet "Panduan" verbatim:
```
CARA MENGISI

Hari       : senin/selasa/rabu/kamis/jumat/sabtu/minggu (huruf kecil).
Jam Ke     : boleh dikosongkan - otomatis lanjutan terakhir hari itu.
Jenis      : "pelajaran" atau "kegiatan" (mis. Upacara/Istirahat).
Jam Mulai/Selesai : format 24 jam "HH:MM", mis. 07:30.
Label      : wajib diisi kalau Jenis = kegiatan (nama kegiatan), dikosongkan kalau pelajaran.

Baris yang jamnya BENTROK (nomor jam ke- ATAU rentang waktu tumpang tindih dgn baris lain di hari yang sama) akan DILEWATI dan dilaporkan - bukan menimpa data yang sudah ada.
```
File `template_jam_belajar.xlsx`.
`processImportJam()`: wajib `tahun_ajaran_id`. Loop (hari(lower), jam_ke raw, jenis, mulai, selesai, label): hari kosong→skip; tak dikenal→"Baris {no}: hari \"{hari}\" tidak dikenali."; jenis default 'pelajaran'; label validasi format `/^\d{1,2}:\d{2}$/`→"Baris {no}: jam mulai/selesai harus format HH:MM (mis. 07:30)."; mulai>=selesai→"Baris {no}: jam selesai harus setelah jam mulai."; jam_ke numerik pakai apa adanya, else auto MAX+1; cek bentrok nomor→"Baris {no}: jam ke-{jamKe} pada hari {hari} sudah ada."; cek tumpang tindih (fungsi SAMA storeJam)→"Baris {no}: waktu {mulai}-{selesai} bentrok dengan \"{label}\" ({waktu}) pada hari {hari}." Message: "Import selesai: {masuk} baris jam masuk, {dilewati} dilewati."

### IMPORT — Struktur Kurikulum
`importAlokasi()` GET→jenis='alokasi'.
`downloadTemplateAlokasi()`: contoh tingkat dari tingkat aktif pertama. Sheet "Struktur Kurikulum"(Tingkat,Mata Pelajaran,JP per Minggu)+sheet Panduan:
```
CARA MENGISI

Tingkat        : kode tingkat SESUAI yang sudah ada di menu Kelola Kelas ({daftar kode}).
Mata Pelajaran : nama PERSIS sesuai tab Mata Pelajaran (tambahkan/import dulu di sana kalau belum ada).
JP per Minggu  : angka. Isi 0 atau kosongkan berarti mapel itu TIDAK diajarkan di tingkat itu (baris dilewati, bukan error).

Baris dgn tingkat/mapel yang tidak dikenali akan DILEWATI dan dilaporkan.
```
File `template_struktur_kurikulum.xlsx`.
`processImportAlokasi()`: wajib `tahun_ajaran_id`. Peta bantu dibangun sekali: kode tingkat aktif + nama-mapel(lower)→id. Loop (tingkat,namaMapel,jp): keduanya kosong→skip; tingkat tak dikenal→"Baris {no}: tingkat \"{tingkat}\" tidak dikenali (cek Kelola Kelas)."; mapel tak ditemukan→"Baris {no}: mata pelajaran \"{nama}\" tidak ditemukan (tambahkan dulu di tab Mata Pelajaran)."; `simpanSel()` (0/kosong=hapus, valid operasi). Message: "Import selesai: {masuk} sel diproses, {dilewati} dilewati."

### `bacaExcelImport()` (private, 3 import di atas)
Validasi: file ada&valid; ekstensi xlsx/xls; maks 5MB (5*1024*1024). Pesan: "Upload gagal: {errorString}"/"File tidak ditemukan."; "Format file harus .xlsx atau .xls."; "Ukuran file terlalu besar. Maksimal 5 MB." Simpan `WRITEPATH.'uploads'` random, memory_limit 512M, max_execution_time 300. Parse `SimpleXLSX`, gagal→"Gagal membaca file Excel: {parseError}". `array_shift` buang header. `finally`: file SELALU dihapus. Return `[rows,null]` atau `[[],RedirectResponse]`.

### TAB 4 — Master Tingkat
`storeTingkat()`: insert `{kode,nama,urutan}`, redirect `tujuanTingkat()`. Gagal→pesan model (TingkatModel: kode required max20 unique, nama required max50). Sukses→"Tingkat \"{nama}\" ditambahkan."
`updateTingkatBatch()`: **`kode` SENGAJA TIDAK ikut diubah** (dipakai kunci relasi kelas.tingkat/kurikulum_alokasi/guru_mata_pelajaran). `is_active` dari checkbox array. Message: "{ok} tingkat diperbarui."
`deleteTingkat(id)`: tidak ada FK dari kelas.tingkat (relasi via kode teks) — controller MANUAL hitung `COUNT(*) FROM kelas WHERE tingkat={kode}`. Dipakai→"Tidak bisa menghapus \"{nama}\" - masih dipakai {n} kelas. Nonaktifkan saja bila sudah tidak dipakai." Sukses→"Tingkat \"{nama}\" dihapus."

---

## 4. MODUL JADWAL PELAJARAN (`JadwalPelajaran.php`)

Dirombak 2026-08-20 mengikuti format resmi sekolah. GRID (baris=jam ke-N, kolom=hari), kode "35K"=guru nomor 35 mengajar mapel berkode K. Kode tanpa angka (mis."F")=mapel TANPA guru tetap (BTAQ Ummi berkelompok).

### `konteks()` (private): `tahunAjaranId` GET fallback aktif; `semester` GET default 'ganjil', dipaksa ganjil kalau bukan ganjil/genap.

### `index()`
`kelasList=getAktifWithTingkat()`. `kelasId` GET fallback kelas pertama. Baris grid = UNION semua jam_ke yang dipakai HARI MANAPUN (bukan 1 hari), di-ksort — supaya hari lebih pendek (Jumat) tetap sejajar. `petaJam[hari][jam_ke]=baris`.
**Panel Progres Kurikulum** (hanya kalau tahunAjaranId&kelas valid): `alokasi=getAlokasiTingkat()`, `terpakai=hitungJpTerpakai()`. Mapel alokasi=0&terpakai=0→skip tampil. Data lain: `batasanTingkat`, `isi=getGridKelas()`, `mapelList`, `guruList=getPengajarAktif()`, `progres`, `piket=getPerHari()`.

### `bacaKode()` (private) — parser kode sel
Normalisasi: uppercase, buang whitespace internal. Kosong→`[null,null,null]`. Regex wajib `/^(\d*)([A-Z]+)$/` — angka opsional depan, huruf wajib belakang. Tidak cocok→"kode \"{raw}\" tidak dikenali (contoh benar: 35K)". Kode huruf tak ada di peta→"kode mata pelajaran \"{kode}\" belum terdaftar". Tanpa angka→`[null,mapelId,null]`. Ada angka tapi guru tak ditemukan→"guru nomor {nomor} tidak ditemukan/tidak aktif". Sukses→`[guruId,mapelId,null]`.

### `simpanGrid()` — simpan grid 1 kelas sekaligus
Wajib tahunAjaranId,kelasId,semester valid→"Tahun ajaran / semester / kelas tidak valid." Peta mapel&guru dibangun sekali. Per `sel[jamId]=raw`: jam tidak ditemukan/ta tidak cocok→skip diam-diam; `posisi="Jam ke-{jamKe} {Hari}"`; `bacaKode()` error→tambah errors, lanjut; mapelId===null→`hapusSlot()`,dihapus++; else `simpanSlot()` (bentrok guru→error) atau tersimpan++. Message: "{tersimpan} slot tersimpan, {dihapus} dikosongkan." + (error) " Beberapa sel dilewati - lihat rincian di atas."

### `salin()`
Wajib sumberTa truthy, sumberSem ganjil/genap→"Sumber salinan belum dipilih." Sumber=tujuan persis→"Sumber dan tujuan tidak boleh sama." `salinDari()`→"{disalin} slot disalin, {dilewati} dilewati (sudah terisi atau jamnya tidak ada di tahun ajaran ini). Hasil salinan bebas diubah/dihapus."

### `simpanPiket()`
**Hapus SEMUA** baris (ta,semester) dulu, insert ulang dari checkbox `piket[hari][]=guru_id` (validasi hari valid HARI, guru unique+filter). Pola "replace all". Message: "{n} penugasan piket disimpan."

### `cetak()` — tampilan cetak semua kelas 1 halaman
`semuaJamKe`+`petaJam` union semua hari. View `jadwal_pelajaran/cetak` dengan `isiSemua=getGridSemuaKelas()`. HTML print-friendly (bukan turunan `templates/index`), tombol "Cetak"(window.print())/"Tutup"(window.close()).

### `import()` GET — info `jumlahKelas`, `jumlahJam`.

### `kerangkaGrid()` (private) — kerangka grid template (hari aktif, kelas, petaJam, semuaJamKe), dipakai BERSAMA `downloadTemplate()` dan `processImport()`.

### `downloadTemplate()`
Tanpa tahunAjaranId→"Tahun ajaran belum dipilih." hariAktif/kelasList kosong→"Isi dulu Jam Belajar (menu Kurikulum) dan data Kelas sebelum membuat template."
Struktur: Baris1 judul; Baris2 "barisHari" (2 kolom kosong+hari diulang per kolom kelas, TIDAK di-merge cell); Baris3 "barisKelas" (Jam ke,Waktu,+nama kelas); Baris data per jam_ke: jam_ke, rentang waktu HH:MM-HH:MM, per(hari×kelas): tidak ada jam→kosong; kegiatan→label pra-terisi; pelajaran→kosong.
Sheet tambahan: "Kode Mapel"(Kode,Mata Pelajaran), "Kode Guru"(Nomor,Nama,Jabatan), "Panduan":
```
CARA MENGISI

1. Isi tiap sel dengan kode: <nomor guru><kode mapel>. Contoh: 35K = guru nomor 35 mengajar mapel K.
2. Kalau mata pelajaran itu tidak punya guru tetap per kelas (mis. BTAQ berkelompok), tulis kode mapelnya saja. Contoh: F
3. Kosongkan sel yang memang tidak ada pelajaran.
4. Sel yang sudah berisi nama kegiatan (Istirahat, Ishoma, dll) JANGAN diubah - itu diatur di menu Kurikulum & Jam Belajar.
5. Daftar kode ada di sheet "Kode Mapel" dan "Kode Guru".
6. Jangan mengubah baris judul, baris HARI, baris nama kelas, dan kolom "Jam ke"/"Waktu".

Baris yang bentrok jam gurunya akan dilewati dan dilaporkan - jadwal lama tidak akan tertimpa diam-diam.
```
File `template_jadwal_pelajaran.xlsx`.

### `processImport()`
Validasi tahunAjaranId&semester. `count(rows)<4`→"File tidak berisi data jadwal. Pakai template yang diunduh dari halaman ini." `kerangkaGrid()` dipanggil ulang.
Baris2=hari, baris3=nama kelas. Loop kolom mulai idx2: kolom kosong total→lewat diam-diam; hari tak dikenal→`kolomTakDikenal[]="hari \"{h}\" tidak dikenali"`; nama kelas kosong/tak match(normalisasi uppercase+buang whitespace)→"kelas \"{nk}\" (kolom hari {H}) tidak ada di data kelas aktif". `$kolom` kosong akhirnya→"Tidak ada kolom kelas yang dikenali. Pastikan baris HARI dan baris nama kelas tidak diubah, dan unduh ulang template kalau data kelas berubah."
Baris data mulai idx3: kolom0=jam_ke(harus>0 else skip diam-diam). Per kolom dikenal: sel kosong→skip; jam tak ada di hari itu→skip diam-diam; jenis='kegiatan'→skip diam-diam ("diatur di menu Kurikulum"). `posisi="{Hari} jam ke-{jamKe}, kelas {nk}"`. `bacaKode()` error sama simpanGrid; mapelId===null→skip diam-diam; `simpanSlot()` bentrok→error+dilewati++.
**Kolom tak dikenal dilaporkan PALING ATAS** (unshift) — dampak lebih besar (1 kolom=1 kelas hilang seharian) daripada 1 sel salah. Format: "KOLOM DILEWATI (seluruh isinya tidak ikut masuk): {list}". Message: "Import selesai: {tersimpan} slot tersimpan, {dilewati} dilewati." + (kolom tak dikenal) " PERHATIAN: {n} kolom tidak dikenali dan seluruh isinya dilewati - lihat rincian di atas."

### `bacaExcel()` (private)
Catatan performa: file jadwal >1.200 sel (24 kelas×5 hari×16 jam), tiap sel cek bentrok guru ke DB. Batas bawaan PHP (120s XAMPP) bisa terlampaui di PC lambat. Aman diulang (slot sama ditimpa, bukan digandakan), batas dinaikkan seperti import Siswa.

### `normalize()` (private): `mb_strtoupper(trim(preg_replace('/\s+/','',$v)))`.

### `perGuru()` — jadwal 1 guru (default pertama) + `beban=getBebanGuru()`.

---

## 5. MODEL-MODEL TERKAIT

### 5.1 `MataPelajaranModel`
`allowedFields=['nama','kode','kelompok','urutan']`. Validation: `mata_pelajaran_id=>'permit_empty|is_natural'` (WAJIB, lihat updateMapelBatch); `nama=>'required|min_length[2]|max_length[100]|is_unique[mata_pelajaran.nama,mata_pelajaran_id,{mata_pelajaran_id}]'`; `kode=>'permit_empty|max_length[10]|is_unique[mata_pelajaran.kode,mata_pelajaran_id,{mata_pelajaran_id}]'`. Pesan: nama.required='Nama mata pelajaran wajib diisi.', min_length='Nama mata pelajaran minimal 2 karakter.', is_unique='Mata pelajaran ini sudah ada.', kode.is_unique='Kode ini sudah dipakai mata pelajaran lain.', kode.max_length='Kode maksimal 10 karakter.'
Method: `getTerurut()` urutan=0 dilempar BELAKANG(false), lalu urutan ASC, nama ASC. `getTerurutPerKelompok()`: kelompok kosong→'Lainnya', 'Lainnya' SELALU paling bawah. `getKelompokTerpakai()`: distinct non-kosong sort. `getDropdown()`. `getPetaKode()`: kode(uppercase)=>baris.

### 5.2 `JamPelajaranModel`
`allowedFields=['tahun_ajaran_id','hari','jam_ke','jam_mulai','jam_selesai','jenis','label']`. Tanpa validationRules.
`const HARI=['senin','selasa','rabu','kamis','jumat','sabtu','minggu']` (7 hari).
`getPerHari($ta)`: 7 hari selalu ada key. `getHariAktif($ta)`: distinct hari ADA baris. `getSlotPelajaran($ta,$hari)`: jenis='pelajaran'. `getBatasanTingkat($ta)`: join jam_pelajaran_tingkat, jam_pelajaran_id=>[tingkat_kode] (kosong=semua tingkat). `setBatasanTingkat()`: delete+insert ulang. `salinDari($sumberTa,$tujuanTa)`: kombinasi(hari,jam_ke) sudah ada di tujuan→dilewati, else insert+salin batasan tingkat.

### 5.3 `KurikulumAlokasiModel`
`allowedFields=['tahun_ajaran_id','tingkat_kode','mata_pelajaran_id','jp_per_minggu']`.
`getMatriks($ta)`: [tingkat][mapel]=jp. `getAlokasiTingkat($ta,$tk)`: [mapel=>jp]. `simpanSel($ta,$tk,$mapelId,$jp)`: **jp<=0→HAPUS baris**, ada→update, tidak→insert. `salinDari()`: skip kombinasi (tingkat,mapel) sudah ada, insertBatch sisanya.

### 5.4 `JadwalPelajaranModel`
`allowedFields=['tahun_ajaran_id','semester','kelas_id','jam_pelajaran_id','mata_pelajaran_id','guru_id']`.
`const HARI` **@deprecated** (TANPA 'minggu'!) — pertahankan utk kode lama, sumber sebenarnya JamPelajaranModel::HARI.
`getGridKelas()`: join mapel(inner)+guru(LEFT). `getGridSemuaKelas()`: keyed [kelas_id][jam_pelajaran_id]. `cekBentrokGuru($guruId,$jamPelajaranId,$semester,$kecualiKelasId=null)`: karena jam_pelajaran_id sudah mengandung(ta+hari+jam ke), cukup pencocokan id — tidak perlu hitung overlap waktu lagi. `hitungJpTerpakai()`: COUNT GROUP BY mapel. `getJadwalGuru()`: urut hari via FIELD(...), jam_ke. `getBebanGuru()`: count group by guru_id (exclude null). `simpanSlot()`: guruId truthy→cek bentrok dulu→pesan "Guru tersebut sudah mengajar {mapel} di kelas {kelas} pada jam yang sama."; cari existing→update/insert; return null sukses. `hapusSlot()`: delete where match 4 kolom. `salinDari($sumberTa,$sumberSem,$tujuanTa,$tujuanSem)`: tahun BEDA→dipetakan via(hari,jam_ke) (jam_pelajaran_id beda antar tahun); tanpa pasangan→dilewati; sudah ada→dilewati juga. `getBebanGuruPerSemester()`: dihitung LANGSUNG dari Jadwal Pelajaran (akurat, bukan manual).

### 5.5 `KalenderAkademikModel`
`allowedFields=['tahun_ajaran_id','judul','kategori','warna','tanggal_mulai','tanggal_selesai','waktu','sasaran','is_libur','keterangan']`. Validation: `judul=>'required|max_length[200]'`('Nama kegiatan wajib diisi.'), `tanggal_mulai=>'required|valid_date'`('Tanggal mulai wajib diisi.'/'Tanggal mulai tidak valid.').
`const WARNA_SARAN=['Libur Nasional'=>'#dc3545','Libur Semester'=>'#dc3545','Asesmen'=>'#0d6efd','Kegiatan'=>'#198754','Rapor'=>'#fd7e14','Ujian Kelas 6'=>'#6f42c1']`.
`getByTahunAjaran($ta)`: urut tanggal_mulai ASC. `sudahAda($ta,$judul,$tanggalMulai)`: cek duplikasi (re-import file sama tidak menggandakan). `getKategoriTerpakai()`: distinct kategori+union WARNA_SARAN keys.

### 5.6 `JadwalPiketModel`
`allowedFields=['tahun_ajaran_id','semester','hari','guru_id']`. `getPerHari($ta,$semester)`: join guru urut nama, keyed hari.

### 5.7 `RiwayatAkademikModel`
`allowedFields=['siswa_id','tahun_ajaran_id','kelas_id','status']`. Tanpa validationRules.
`getByTahunAjaran($ta,$kelasId=null,$status=null)`: join siswa,kelas(LEFT via ra.kelas_id) DAN join kedua ke kelas (kt, LEFT via s.kelas_id=kelas SEKARANG) untuk `kelas_tujuan_nama` — "Naik ke {kelas tujuan}" di Arsip. `getLulusByTahunAjaran()`: shortcut status='lulus'. `existsForSiswaTahun()`: cek duplikasi. `getRiwayatSiswa()`: seluruh riwayat 1 siswa, tahun DESC.

### 5.8 `TingkatModel`
`allowedFields=['kode','nama','urutan','is_active']`. Validation: `kode=>'required|max_length[20]|is_unique[tingkat.kode,tingkat_id,{tingkat_id}]'`('Kode tingkat wajib diisi.'/'Kode tingkat ini sudah dipakai.'), `nama=>'required|max_length[50]'`('Nama tingkat wajib diisi.').
`getAktif()`: is_active=1, urutan ASC,kode ASC. `getDropdown()`: aktif saja. `getLabelMap()`: SEMUA (termasuk nonaktif) — data lama yg tingkatnya sudah nonaktif. `labelFallback($kode)` static: kosong→'Semua Tingkat'; numerik→'Kelas '.kode; else kode apa adanya.

---

## 6. MODUL KALENDER AKADEMIK (`KalenderAkademik.php`)

Sumber acuan file "KALDIK DAN AGENDA KEGIATAN SEKOLAH". Data diteruskan ke Mobile-app.

### `tahunAjaranId()` (private) — sama pola resolveTahunAjaranId Kurikulum.
### `index()` — kirim `agenda`, `kategoriList`, `warnaSaran`.

### `rentangWajar(int $tahunAjaranId): ?array` (private) — INTI validasi anti-salah-tahun
Rentang tanggal WAJAR 1 tahun ajaran, buffer ~1 bulan tiap sisi. Ditambahkan 2026-09-07 setelah laporan user: agenda "Desember 2025" ikut nongol di TA 2026/2027 — sekelas dengan bug parser tanggal 2026-08-26 (lihat §7). Baris LAMA yang sudah terlanjur salah SEBELUM perbaikan TIDAK ikut terkoreksi otomatis, harus dibetulkan manual oleh TU.

**Algoritma**: nama TA tidak match `/^(\d{4})\/(\d{4})$/`→return null (validasi dilewati). Match: `awal="{tahun1}-06-01"` (1 Juni tahun pertama), `akhir="{tahun2}-07-31"` (31 Juli tahun kedua). Return `{nama,awal,akhir}`.

### `diLuarRentangWajar($tanggal,$rentang): bool`
`$rentang!==null && ($tanggal<$rentang['awal'] || $tanggal>$rentang['akhir'])`. Perbandingan string leksikal Y-m-d. **`$rentang` null → SELALU false → validasi TIDAK diterapkan.**

### `dataDariRequest()` (private) — kumpulkan field form.

### `store()`
1. Tanpa `tahun_ajaran_id`→"Tahun ajaran belum dipilih."
2. `tanggal_selesai` terisi & <`tanggal_mulai`→"Tanggal selesai tidak boleh sebelum tanggal mulai."
3. `diLuarRentangWajar()`→"Tanggal mulai ({tgl}) di luar rentang wajar tahun ajaran {nama}. Periksa lagi - mungkin tahunnya salah ketik."
4. Insert; gagal model→error gabungan.
5. Sukses→"Agenda \"{judul}\" ditambahkan."

### `update(id)` — alur IDENTIK store() (3 validasi sama). Sukses→'Agenda diperbarui.' (tanpa judul, beda gaya dari store).

### `delete(id)` — tidak ada→"Agenda tidak ditemukan." Sukses→"Agenda \"{judul}\" dihapus."

### `import()` GET.

### `downloadTemplate()`
Header: `['Tanggal','Tanggal Selesai (opsional)','Kegiatan','Kategori','Waktu','Sasaran','Libur','Keterangan']`. 5 contoh baris (rentang, single, tanggal lintas tahun, "&" 2 acara, format lama 2 kolom).
Sheet "Panduan":
```
CARA MENGISI

Kolom "Tanggal" boleh ditulis seperti di kalender sekolah - tidak perlu dipecah:
    10 Juli 2026            -> satu hari
    13-17 Juli 2026         -> 13 sampai 17 Juli
    25 Mar-03 April 2027    -> lintas bulan
    21 Des - 02 Jan 2027    -> lintas tahun (Desember otomatis dibaca 2026)
    18 & 25 Juli 2026       -> DUA acara terpisah, masing-masing 1 hari
    2026-07-13              -> format lama, tetap diterima

Tanggal Selesai : boleh dikosongkan. Isi hanya kalau memakai format lama 2 kolom.
Kegiatan        : wajib, nama kegiatan.
Kategori        : bebas diisi (Libur Nasional, Asesmen, Kegiatan, Rapor, dll).
Waktu           : bebas, contoh "08.00 - 11.00".
Sasaran         : untuk siapa, contoh "Kelas 6" atau "Kelas 1-6".
Libur           : isi "ya" jika hari itu tidak ada kegiatan belajar, selain itu "tidak".
Keterangan      : catatan tambahan, boleh dikosongkan.

YANG AKAN DILEWATI DAN DILAPORKAN (bukan ditebak):
    Okt/Nov 2026          - tidak ada tanggalnya
    27-05 Juni 2027       - tanggal awal lebih besar dari akhir di bulan yang sama
    21 Des 25 - 02 Jan 26 - tahun ditulis 2 digit, tidak jelas tahunnya
Perbaiki penulisannya lalu import ulang - agenda yang sudah masuk tidak akan terduplikat.
```
File `template_kalender_akademik.xlsx`.

**KETIDAKSESUAIAN DOKUMENTASI**: `kalender_akademik/import.php` (view) menampilkan instruksi BASI ("Urutan kolom wajib tetap...", format YYYY-MM-DD saja) — TIDAK menyebutkan dukungan format Indonesia yang sesungguhnya sudah diimplementasi. Untuk porting: ikuti PERILAKU SESUNGGUHNYA (controller+template+helper), bukan teks halaman import — kecuali user minta replikasi 100% termasuk bug dokumentasi.

### `processImport()`
1. Wajib `tahun_ajaran_id`.
2. Validasi file (pola sama modul lain).
3. memory 512M, exec 300.
4. Baca, buang header. `helper('date')`.
5. `$rentang=rentangWajar($ta)`.
6. Loop: kolom0=tanggal,2=judul. Keduanya kosong→skip. Judul kosong(tgl ada)→"Baris {no}: nama kegiatan wajib diisi."
   - **Kolom1 TERISI (format lama 2 kolom)**: `mulai=parseTanggal(kolom0)`,`selesai=parseTanggal(kolom1)`. mulai null→"Baris {no}: tanggal mulai \"{tgl}\" tidak dikenali." selesai<mulai→"Baris {no}: tanggal selesai sebelum tanggal mulai." `$periode=[[mulai,selesai]]` (SATU periode).
   - **Kolom1 KOSONG (kalender Indonesia)**: `$periode=parse_indonesian_date_range($tglMentah)` (0/1/2 periode). Kosong→"Baris {no}: tanggal \"{tgl}\" tidak dikenali atau ambigu - perbaiki penulisannya lalu import ulang."
   - `$dasar`: judul,kategori(kosong→null),warna(WARNA_SARAN[kategori] kalau ada else null),waktu(kosong→null),sasaran(kosong→null),is_libur(in_array lowercase trim ['ya','y','1','true']→1 else 0),keterangan(kosong→null).
   - **Untuk TIAP periode**: cek rentang wajar→"Baris {no}: tanggal \"{tgl}\" ({mulai}) di luar rentang wajar tahun ajaran {nama} - kemungkinan tahunnya salah ketik." (dilewati++, continue). Cek duplikasi `sudahAda()`→duplikat++,continue (tidak error, hanya dihitung). Insert. masuk++.
7. Message: "Import selesai: {masuk} agenda masuk" + (duplikat>0)", {duplikat} sudah ada (dilewati)" + ", {dilewati} tidak terbaca."
8. Exception→'Error: {msg}'. finally: file temp dihapus.

### `parseTanggal(mixed $v): ?string` (private) — SATU tanggal, dipakai kalau kolom "Tanggal Selesai" terisi
**BUG NYATA & PERBAIKAN**: SENGAJA memakai `parse_indonesian_date_range()` lalu ambil mulai, BUKAN `date_create()` seperti versi lama. `date_create("7 Juni 2027")` mengembalikan `2026-06-07` — mundur 1 tahun tanpa peringatan (2027 ditelan jadi jam 20:27). Bug nyata membuat 4 dari 42 agenda resmi sekolah masuk tahun keliru (2026-08-26). Implementasi: `parse_indonesian_date_range($v)`, return `periode===[]?null:periode[0]['mulai']`.

---

## 7. HELPER `parse_indonesian_date_range()`/`parse_indonesian_date()` (`app/Helpers/date_helper.php`)

### `indonesian_month_number(string $name): ?int`
Peta nama/singkatan bulan (case-insensitive, trim): pebruari→2, ags/agt/agu/aug→8, nopember→11, sept→9, english short (oct→10,dec→12). Tidak dikenal→null.

### `parse_indonesian_date(string $date): ?string`
1. Kosong→null.
2. "DD Bulan YYYY" (`/^(\d{1,2})\s+([A-Za-z]+)\s+(\d{4})$/u`): bulan dikenal & checkdate valid→Y-m-d; **nama bulan tak dikenali/tanggal invalid→null LANGSUNG, TIDAK diteruskan ke strtotime()**.
3. Normalisasi `/`→`-`.
4. "DD-MM-YYYY" (1-2 digit) via regex, checkdate.
5. **Fallback**: string MASIH mengandung huruf→**null (DITOLAK)** — kunci pencegahan bug: `strtotime('7 Juni 2027')` TIDAK error tapi "Jun"→Juni, sisa "i" memutus token, "2027" ditelan jadi JAM 20:27, tahun jatuh ke tahun berjalan→hasil `2026-06-07` salah 1 tahun TANPA peringatan. Bug nyata 2026-08-26, 4/42 baris agenda salah tahun.
6. Tanpa huruf→`strtotime()` fallback terakhir (format numerik lain).

### `parse_indonesian_date_range(mixed $raw): array` — return `list<{mulai,selesai:?string}>`
1. Kosong→[].
2. Serial Excel (angka>1): `gmdate('Y-m-d',((int)$s-25569)*86400)`.
3. Buang isi dalam kurung, samakan en/em dash/minus jadi `-`, rapatkan spasi.
4. **"DUA tanggal terpisah dgn &"**: `/^(\d{1,2})\s*[&]\s*(\d{1,2})\s+([A-Za-z]+)\s+(\d{4})$/u` mis "18 & 25 Juli 2026"→2 periode terpisah 1 hari (selesai=null). Bulan tak dikenal/invalid→[].
5. **"rentang lintas bulan/tahun"**: `/^(\d{1,2})\s+([A-Za-z]+)\s*-\s*(\d{1,2})\s+([A-Za-z]+)\s+(\d{4})$/u` mis "25 Mar-03 April 2027"/"21 Des - 02 Jan 2027". Tahun ditulis sekali di akhir. **Lintas tahun**: bulan1>bulan2(Des=12>Jan=1)→tahun awal=tahun akhir-1. Validasi checkdate & selesai>=mulai, else [].
6. **"rentang 1 bulan 1 tahun"**: `/^(\d{1,2})\s*-\s*(\d{1,2})\s+([A-Za-z]+)\s+(\d{4})$/u` mis "13-17 Juli 2026". selesai>=mulai; awal>akhir di bulan sama→AMBIGU, ditolak [].
7. **Fallback tunggal**: `parse_indonesian_date($s)`, berhasil→1 periode selesai=null.
8. Tidak cocok→[] ("tidak dikenali atau ambigu").

**Contoh SENGAJA DITOLAK**: "Okt/Nov 2026" (tanpa tanggal); "27-05 Juni 2027" (awal>akhir bulan sama, ambigu); "21 Des 25 - 02 Jan 26" (tahun 2 digit, regex wajib 4 digit).

---

## 8. VIEWS — field form, tombol, teks verbatim

### 8.1 `kurikulum/index.php`
Card pemilih TA (auto-submit), teks: "Semua isian di halaman ini berlaku per tahun ajaran, jadi mengubah kurikulum tahun ini **tidak merusak data tahun sebelumnya**." Belum ada TA: "Belum ada tahun ajaran. Tambahkan dulu di menu Akademik » Tahun Ajaran." Tab nav: Mata Pelajaran/Jam Belajar/Struktur Kurikulum.

**Tab Mata Pelajaran**: tombol Import Excel. Teks: "**Kode** = huruf singkat mapel (mis. K untuk PJOK), dipakai nanti saat mengisi Jadwal Pelajaran (contoh 35K = guru nomor 35 mengajar PJOK). Boleh dikosongkan... **Kelompok** & **Urutan** mengikuti dokumen Struktur Kurikulum sekolah..." Form tambah: Kode(uppercase max10), Nama(required), Kelompok(datalist), Urutan(number min0 default0), "Tambah". Tabel: Kode,Nama,Kelompok,Urutan,Hapus. Kosong: "Belum ada mata pelajaran."
**Trik HTML**: `<form id="formMapel">` DI LUAR `<table>`, input di sel `<tr>` menunjuk via `form="formMapel"` — `<form>` dalam `<tr>` HTML tidak sah, browser memindahkan keluar tabel. Tombol "Simpan Perubahan" (submit formMapel). Hapus per-baris→SweetAlert2 'Hapus?'/'{nama} akan dihapus.'/'Ya, Hapus'/'Batal'.

**Tab Struktur Kurikulum**: Import Excel + form "Salin dari tahun..." Teks: "**Opsional**... Kosongkan sel = mapel itu tidak diajarkan di tingkat tersebut." Mapel/tingkat kosong: "Isi dulu Mata Pelajaran dan Tingkat sebelum mengatur ini." Matriks: baris=mapel(grup per kelompok), kolom=tingkat+Total, footer JUMLAH JP/MINGGU. Input number min0 max99 `alokasi[{tk}][{mapelId}]`. Total live JS.

**Tab Jam Belajar**: Import Excel+Salin. Teks: "Susunan jam ini berlaku untuk semua kelas. Hari yang tidak diisi dianggap libur... Pilih Kegiatan untuk jam istirahat/ishoma/upacara — cukup diisi sekali, otomatis muncul di semua kelas."
Form tambah jam (AJAX tanpa reload): Hari,Mulai,Selesai(required),Jenis(select),Nama Kegiatan(disabled kecuali kegiatan). Hidden `jam_ke=0`. Alert error inline `#alertTambahJam` (d-none default) — BUKAN popup. Sukses: baris disisip JS tanpa reload, form dikosongkan KECUALI "Hari" (staf sering isi banyak jam hari sama), collapse dibuka otomatis, badge bertambah.
Card per hari collapsible (default TERTUTUP), badge jumlah, tabel Ke/Mulai/Selesai/Jenis/Nama Kegiatan/Hapus, baris kegiatan class `table-warning`. Kosong: "Belum ada jam." Tombol "Simpan Perubahan" (formJam batch). Hapus SweetAlert2 via event delegation ke document.

### 8.2 `kurikulum/import.php`
1 view 3 jenis (mapel/jam/alokasi), teks catatan per jenis (lihat §3 IMPORT). Langkah umum: "1. Download template... 2. Isi sesuai contoh... 3. Unggah kembali..." File input accept .xlsx,.xls required, hint "Format .xlsx atau .xls, maksimal 5 MB." Tombol "Import Sekarang"/"Batal".

### 8.3 `jadwal_pelajaran/index.php`
Pemilih: TA,Semester,Kelas (auto-submit). Tombol Import Excel/Cetak(_blank)/Per Guru.
Belum TA: "Belum ada tahun ajaran..." Jam belum diatur: "Jam belajar untuk tahun ajaran ini belum diatur. Buka menu Kurikulum & Jam Belajar » Jam Belajar terlebih dahulu." Belum kelas: "Belum ada kelas aktif..."
**Panel Progres Kurikulum** (collapsible tertutup default): `{terpakai}/{alokasi}` warna hijau(pas)/merah(kurang {n})/kuning(lebih {n}). Kolom TOTAL.
**Grid utama**: "Isi tiap sel dengan `<nomor guru><kode mapel>`, contoh 35K. Tanpa guru tetap? Cukup kode mapelnya saja, contoh F. Kosongkan sel untuk menghapus isinya."
Tabel `table-layout:fixed`, kolom Jam(42px)/Waktu(78px) — **BUG TAMPILAN DIPERBAIKI**: width dinaikkan 36→42 & 68→78 + overflow:hidden (2026-09-07) — laporan user angka jam & rentang waktu ('07:20-07:50', 11 karakter) bocor visual ke kolom sebelah tanpa overflow:hidden. WAJIB diperhatikan porting WPF/WinForms DataGrid — lebar cukup ATAU trim/wrap eksplisit.
Baris kegiatan→sel `bg-warning-subtle text-body-secondary fst-italic`; dibatasi tingkat lain & tidak berlaku kelas ini→`–` di `bg-light text-muted`.
Sel input: uppercase, class `sel-kode`, validasi LIVE JS, highlight is-invalid/is-valid, teks bantu `.sel-info`. Enter=pindah fokus bawah. Cek bentrok guru antar-kelas HANYA di SERVER. Tombol "Simpan Jadwal","Salin dari...".
**Panel Guru Piket**: checkbox per hari per guru (BUKAN select multiple — "Select multiple mengharuskan tahan Ctrl; sekali salah klik seluruh pilihan hilang tanpa peringatan"). "Centang guru yang bertugas piket. Boleh lebih dari satu per hari." Tombol "Simpan Piket".
Modal "Daftar Kode": Kode Mapel/Nomor Guru. Modal "Salin Jadwal": "Menyalin jadwal seluruh kelas dari sumber yang dipilih. Slot yang sudah terisi tidak akan ditimpa..." Dari TA/Dari Semester(default LAWAN semester aktif). Tombol "Salin Sekarang".

### 8.4 `jadwal_pelajaran/cetak.php`
HTML print murni, font 9px, tombol Cetak/Tutup. Judul "JADWAL PELAJARAN {ta}"+"Semester {semester}". Header 2 baris(rowspan Jam/Waktu, colspan hari, nama kelas). Baris kegiatan colspan seluas kelas class `.kegiatan`(kuning italic). Baris kosong class `.kosong`. Baris "Piket" akhir. Legend Kode Mata Pelajaran/Kode Pengajar.

### 8.5 `jadwal_pelajaran/import.php`
Info bar: "Import untuk: {ta} · Semester {s} · {n} kelas · {n} baris jam". Langkah: "1. Unduh template... bentuknya sama seperti file jadwal Excel sekolah... 2. blok isi selnya lalu salin, dan tempel... 3. Simpan, unggah kembali." Format sel: `<nomor guru><kode mapel>`. Catatan: "Nama kelas & daftar jam pada template dibuat otomatis..."; "Sel yang kodenya tidak dikenali, atau membuat guru bentrok, akan dilewati dan dilaporkan — jadwal lama tidak tertimpa diam-diam."; "Slot yang sudah ada isinya akan diperbarui sesuai isi file."

### 8.6 `jadwal_pelajaran/per_guru.php`
Filter TA/Semester/Guru (label "#{nomor} {nama} ({jam} jam)"). Tombol "Kembali ke Grid". Tabel jadwal per hari. Kosong: "Guru ini belum punya jadwal mengajar pada semester ini." Panel Beban Mengajar: guru 0 jam abu-abu, footer: "Guru dengan 0 jam berarti belum dijadwalkan mengajar pada semester ini."

### 8.7 `kalender_akademik/index.php`
Filter TA + Import Excel. **Kalender visual**: navigasi bulan, grid 7×6, libur `bg-danger-subtle text-danger fw-bold`, agenda non-libur `bg-primary-subtle text-primary fw-semibold`, hari ini border biru. Klik→detail bawah. Auto-pindah bulan agenda terdekat kalau hari ini kosong.
**Form Tambah**: Nama Kegiatan*,Mulai*,Selesai(opsional),Kategori(datalist),Warna(color, default #198754, auto ikut WARNA_SARAN kecuali user ubah manual),Waktu,Sasaran,Keterangan,checkbox "Hari libur (tidak ada kegiatan belajar)". Tombol "Tambah Agenda".
**Daftar per bulan** (dikelompokkan tanggal_mulai — lintas bulan muncul SEKALI di bulan mulai). Card collapsible: bulan berjalan(ada isi) dibuka otomatis, else terdekat depan, else terakhir. Tombol "Buka Semua"/"Tutup Semua". Tabel: Tanggal(+s/d selesai),Kegiatan(bullet warna+badge Libur merah+waktu+keterangan italic),Kategori,Sasaran,Aksi. Kosong total: "Belum ada agenda. Tambahkan lewat form di samping, atau import dari Excel." Hapus SweetAlert2 'Hapus Agenda?'.

### 8.8 `kalender_akademik/import.php`
(Lihat ketidaksesuaian §6.) Info: "Import untuk tahun ajaran: {ta}". Kotak: "**Urutan kolom wajib tetap:** Tanggal Mulai | Tanggal Selesai | Kegiatan | Kategori | Waktu | Sasaran | Libur | Keterangan" — TEKS BASI. Catatan lain: format YYYY-MM-DD, Libur='ya', kategori dikenal dapat warna otomatis, tanggal invalid dilewati&dilaporkan, "Import bersifat menambah, bukan mengganti. Agenda lama tidak terhapus."

### 8.9 `akademik/index.php`
`page_actions`: badge "TA Aktif: {nama}", tombol "Arsip Historis"/"Rekap Lulusan". Tidak ada TA aktif: "**Tidak ada Tahun Ajaran aktif.** Atur Tahun Ajaran terlebih dahulu." Tidak ada siswa aktif: "Tidak ada siswa aktif ditemukan."
2 tab (1 halaman): "Kenaikan Kelas"/"Kelulusan".
**Tab Kenaikan Kelas**: dropdown "Kelas Tujuan"(required), tombol "Proses Naik Kelas". Info: "Pilih siswa dan kelas tujuan lalu tekan Enter atau klik tombol Proses Naik Kelas..." Per kelas: card checkbox select-all+tabel siswa(checkbox,NIS,Nama,JK badge).
**Tab Kelulusan**: "Pilih siswa yang akan diluluskan. Data diarsipkan pada TA {nama}."+"...tekan Enter atau klik tombol Proses Kelulusan..." Tombol hijau.
JS: submit diblok 0 siswa→SweetAlert warning "Pilih minimal satu siswa." Konfirmasi: SweetAlert2 'Proses Kelulusan?'/'{n} siswa akan diluluskan dan diarsipkan.' (hijau #198754); 'Proses Kenaikan Kelas?'/'{n} siswa akan dinaikkan kelas.' (biru #0d6efd).

### 8.10 `akademik/arsip.php`
Filter TA(required),Kelas(opsional),Status(opsional). Tabel: checkbox,No,NIS,Nama,JK,Kelas,Status(badge "Naik ke {kelas tujuan}"/biru, "Lulus"/hijau, lain/abu), Aksi(Detail). Toolbar bulk-delete tampil tapi endpoint SELALU balas warning: **"Riwayat akademik tidak dapat dihapus karena dipakai sebagai arsip permanen."** (fitur UI ada, backend SENGAJA menolak — replikasi persis: tombol ada, hasil selalu gagal).

### 8.11 `akademik/rekap_lulusan.php`
Filter TA(required). Tabel: checkbox,No,NIS,Nama,JK,Kelas Terakhir,Asal Sekolah,Aksi. Sama pola bulk-delete — endpoint selalu balas: **"Rekap lulusan tidak dapat dihapus karena dipakai sebagai arsip permanen."**

---

## 9. MODUL AKADEMIK (`Akademik.php`)

### `index()`
Tanpa TA aktif→view data kosong. Siswa `status='aktif'` (`getAktifWithKelas()`), kelompok per kelas_id (tanpa kelas→key 0 "Tanpa Kelas"), urut grup by tingkat (uasort). `kelasList`=semua kelas urut tingkat lalu nama.

### `prosesNaikKelas()` — TRANSAKSI DB
1. Tanpa TA aktif→"Tidak ada tahun ajaran aktif. Silakan aktifkan terlebih dahulu."
2. `siswa_ids[]` kosong→WARNING "Tidak ada siswa yang dipilih."
3. `kelas_tujuan` kosong→"Kelas tujuan wajib dipilih."
4. `transStart()`. Per siswa: harus ditemukan&status='aktif' (else skip diam-diam, mungkin race/sudah diproses).
   - Simpan riwayat DI KELAS LAMA HANYA kalau BELUM ADA (existsForSiswaTahun)→insert `{siswa_id,ta,kelas_id:LAMA,status:'naik'}`.
   - Update siswa: `kelas_id=kelasBaruId` (status TETAP 'aktif').
5. Gagal→"Terjadi kesalahan saat memproses kenaikan kelas." Sukses→"{processed} siswa berhasil dinaikkan kelas."

### `prosesKelulusan()` — TRANSAKSI DB
1. Sama cek TA aktif & siswa_ids kosong (pesan sama).
2. Per siswa aktif: cek riwayat existing (siswa+TA) — **SUDAH ADA** (mis. sebelumnya 'naik' di TA sama)→**UPDATE** jadi status='lulus' (bukan insert baru — cegah duplikat unique(siswa_id,ta)). **BELUM ADA**→insert `{siswa_id,ta,kelas_id: kelas siswa saat ini ?? 0, status:'lulus'}`.
   - Update siswa: `status='lulus'`, **`kelas_id=null`**.
3. Gagal→"Terjadi kesalahan saat memproses kelulusan." Sukses→"{processed} siswa berhasil diluluskan dan diarsipkan."

### `arsip()` GET
Filter: tahun_ajaran_id(wajib>0 utk tampil), kelas_id(opsional), status(opsional aktif/naik/lulus). Data: tahunOptions/kelasOptions, riwayat(getByTahunAjaran).

### `rekapLulusan()` GET
Filter tahun_ajaran_id. `lulusan=getLulusByTahunAjaran()`.

### `arsipBulkDelete()`/`rekapBulkDelete()` POST
**KEDUANYA selalu gagal SENGAJA** — tidak sentuh DB, langsung `redirect()->back()->with('warning',...)`: arsipBulkDelete→"Riwayat akademik tidak dapat dihapus karena dipakai sebagai arsip permanen."; rekapBulkDelete→"Rekap lulusan tidak dapat dihapus karena dipakai sebagai arsip permanen." Toolbar bulk-delete UI adalah "vestigial" — porting: tombol boleh ada, behaviour tetap menolak dengan pesan sama.

---

## 10. Perilaku Halus / Edge Case yang Mudah Terlewat

### A. Bug Kalender Akademik — tahun nyasar
1. **Bug historis diperbaiki (2026-08-26)** — `parse_indonesian_date()`: `date_create("7 Juni 2027")`→`2026-06-07` (mundur 1 tahun, tanpa peringatan). 4/42 baris agenda salah tahun. Fix: fallback strtotime dibatasi string TANPA huruf; nama bulan tak dikenali/format salah→null langsung.
2. `KalenderAkademik::parseTanggal()` SENGAJA pakai `parse_indonesian_date_range()` bukan `date_create()` untuk hindari bug ini.
3. **Lapis kedua — `rentangWajar()`/`diLuarRentangWajar()`** (2026-09-07) setelah laporan "Desember 2025" nongol di TA 2026/2027. Kelas bug SAMA tapi lewat jalur lain (salah ketik tahun manual). Solusi: SETIAP tanggal (manual maupun import) divalidasi terhadap rentang wajar TA (`{tahun1}-06-01` s/d `{tahun2}-07-31`). Di luar rentang DITOLAK.
4. **PENTING**: baris LAMA yang sudah salah SEBELUM perbaikan TIDAK ikut terkoreksi otomatis — harus dibetulkan MANUAL. Migrasi ke SQLite TIDAK BOLEH otomatis "memperbaiki" tanggal keliru — bawa apa adanya.
5. `rentangWajar()` HANYA berfungsi kalau nama TA match `YYYY/YYYY` (dipaksa di `TahunAjaran::store()`, BUKAN di model validationRules). Format tak cocok→null→validasi rentang di-skip diam-diam.

### B. Bug Tampilan Jadwal Pelajaran — angka Jam/Waktu keluar kolom
`jadwal_pelajaran/index.php` (~baris 156-161): width 36→42 & 68→78 + overflow:hidden (2026-09-07). table-layout:fixed tanpa overflow:hidden membuat konten sedikit lebih lebar "bocor" ke kolom sebelah. Porting: kolom Jam/Waktu WAJIB lebar memadai atau text-trimming eksplisit.

### C. "mata_pelajaran_id WAJIB ikut dikirim" (update mata pelajaran)
CI4 `is_unique` placeholder substitution — tanpa PK di `$data`, placeholder gagal terisi, `is_unique` cek SELURUH tabel termasuk baris sendiri. Tidak relevan teknis di .NET, tapi maknanya WAJIB: aturan unique saat update HARUS exclude baris ITU SENDIRI.

### D. Aturan bentrok Jam Belajar — HARUS identik jalur manual & import
`cekBentrokNomorJam()`/`cekTumpangTindihJam()` dipisah (2026-09-07) supaya dipakai ulang `processImportJam()` — WAJIB selalu identik. Algoritma: bentrok nomor=COUNT(ta,hari,jam_ke)>0; tumpang tindih=`mulai<selesai_baru AND selesai>mulai_baru` (AND, berlaku walau jam_ke beda). **KRITIS**: `updateJamBatch()` (edit massal tabel) TIDAK memanggil kedua fungsi ini — hanya cek `mulai<selesai`. Inkonsistensi nyata — WAJIB direplikasi apa adanya (bukan bug untuk diperbaiki, kecuali diminta eksplisit).

### E. Cek Bentrok Guru — disederhanakan pasca-restrukturisasi
`cekBentrokGuru()`: karena jam_pelajaran_id sudah mengandung(ta+hari+jam ke), cukup pencocokan id, tidak perlu overlap waktu lagi. 1 guru 2 kelas pada jam_pelajaran_id SAMA=bentrok; jam_pelajaran_id beda (meski overlap waktu, jarang terjadi)=TIDAK terdeteksi.

### F. Kode grid jadwal "35K"/"F"
Regex `^(\d*)([A-Z]+)$` — nomor opsional, huruf wajib, tanpa spasi. Tanpa nomor = mapel TANPA guru tetap (BTAQ Ummi berkelompok, FITUR bukan bug). Sel dikosongkan = HAPUS slot (`hapusSlot()`). Baris kegiatan TIDAK BISA diisi lewat grid/import — auto muncul dari jam_pelajaran, import skip diam-diam sel di baris kegiatan.

### G. Import Jadwal — kolom tak dikenal dilaporkan PRIORITAS
Dampak lebih besar (1 kolom=1 kelas hilang seharian) daripada 1 sel salah. Pesan di-unshift ke depan daftar error, prefix "KOLOM DILEWATI (seluruh isinya tidak ikut masuk): ..." + ringkasan "PERHATIAN: {n} kolom tidak dikenali...". Urutan prioritas tampilan ini harus direplikasi.

### H. Alokasi Kurikulum — 0/kosong = HAPUS bukan simpan 0
`simpanSel()`: menjaga beda makna "tidak diajarkan" vs "diajarkan 0 jam". Jangan pakai kolom nullable default 0 sebagai representasi "tidak diajarkan" — pertahankan pola "row absent = not taught".

### I. Master Tingkat — `kode` adalah kunci relasi TEKS (bukan FK numerik)
`kelas.tingkat`,`kurikulum_alokasi.tingkat_kode`,`guru_mata_pelajaran.tingkat` menyimpan `tingkat.kode` (VARCHAR) apa adanya — BUKAN FK ke `tingkat_id`. Konsekuensi: `deleteTingkat()` MANUAL hitung pemakaian via `SELECT COUNT(*) FROM kelas WHERE tingkat={kode}` (tidak ada FK constraint DB). `updateTingkatBatch()`: kode SENGAJA tidak bisa diubah batch (hanya nama/urutan/is_active). UI tab "Master Tingkat" dipindah ke menu Kelas (2026-08-27) tapi endpoint TETAP di `Kurikulum.php`.

### J. Open-redirect guard `tujuanTingkat()`
POST `kembali_ke` HANYA terima literal 'kelas' (exact match), whitelist — bukan path bebas.

### K. FK RESTRICT vs CASCADE — efek beda
Hapus **Mata Pelajaran** dipakai jadwal→FK RESTRICT→DITOLAK "Tidak bisa menghapus \"{nama}\" - masih dipakai di Jadwal Pelajaran. Hapus jadwalnya dulu." Hapus **Jam Pelajaran** dipakai jadwal→FK **CASCADE**→DIIZINKAN, semua baris jadwal ikut terhapus (dihitung dulu, dilaporkan "{n} isian jadwal pada jam tersebut ikut terhapus."). 2 perilaku SANGAT BEDA — jangan disamakan pola generik.

### L. Salin lintas TA — 3 varian model beda
`JamPelajaranModel::salinDari()`: skip (hari,jam_ke) sudah ada. `KurikulumAlokasiModel::salinDari()`: skip (tingkat,mapel) sudah ada. `JadwalPelajaranModel::salinDari()`: LEBIH KOMPLEKS — TA sama(beda semester)→jam_pelajaran_id langsung sama; TA BEDA→dipetakan via(hari,jam_ke) karena jam_pelajaran_id beda per TA. Tanpa pasangan/sudah terisi→dilewati.

### M. SweetAlert2 hapus — pola konsisten
`e.preventDefault()`, `Swal.fire({title,html,icon:'warning',showCancelButton:true,confirmButtonText:'Ya, Hapus',cancelButtonText:'Batal',confirmButtonColor:'#d33'})`, baru `self.submit()`. Nama entitas selalu bold via `data-nama`.

### N. Ketidaksesuaian dokumentasi UI vs perilaku (Kalender Akademik Import)
Lihat §6/§8.8 — teks `import.php` TIDAK mencerminkan kemampuan sesungguhnya (parser fleksibel). Porting ikuti PERILAKU (controller+helper), bukan teks halaman — atau klarifikasi ke user kalau ingin replikasi 100% termasuk bug dokumentasi ini.

### O. `is_libur` boolean tapi TINYINT, checkbox longgar di import
Form manual: checkbox→1/0. Import: kolom "Libur" terima `'ya','y','1','true'` (lowercase trim) sebagai TRUE, selain itu FALSE. Harus direplikasi persis daftarnya (4 varian), bukan cuma `==='ya'`.

### P. Riwayat Akademik — kelas_id NOT NULL tapi bisa 0
`prosesKelulusan()`: siswa tanpa kelas_id (null)→riwayat disimpan `kelas_id=>siswa['kelas_id']??0` — literal 0 (kolom NOT NULL, tanpa FK eksplisit jadi 0 tidak melanggar constraint), tapi LEFT JOIN kelas di `getByTahunAjaran()` tidak match→tampil "Tanpa Kelas"/kosong.

---

Semua sumber dibaca TUNTAS dari (path absolut, tidak ada perubahan dilakukan):
- `app/Controllers/Kurikulum.php, JadwalPelajaran.php, KalenderAkademik.php, Akademik.php`
- `app/Models/MataPelajaranModel.php, JamPelajaranModel.php, KurikulumAlokasiModel.php, JadwalPelajaranModel.php, KalenderAkademikModel.php, JadwalPiketModel.php, RiwayatAkademikModel.php, TingkatModel.php, TahunAjaranModel.php, GuruModel.php(potongan), KelasModel.php(potongan)`
- `app/Views/kurikulum/index.php, import.php`
- `app/Views/jadwal_pelajaran/index.php, cetak.php, import.php, per_guru.php`
- `app/Views/kalender_akademik/index.php, import.php`
- `app/Views/akademik/index.php, arsip.php, rekap_lulusan.php`
- `app/Database/Migrations/` — 11 file migrasi terkait
- `app/Helpers/date_helper.php`
- `app/Config/Routes.php` (baris 100-198)
- `app/Controllers/TahunAjaran.php` (potongan validasi regex nama)
