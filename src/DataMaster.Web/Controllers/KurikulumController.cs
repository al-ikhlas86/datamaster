using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DataMaster.Data;
using DataMaster.Data.Entities;
using DataMaster.Web.Models.Kurikulum;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Controllers;

// Port 1:1 dari app/Controllers/Kurikulum.php - lihat 03-akademik-jadwal.md §3 & §5.
// 3 tab: Mata Pelajaran, Struktur Kurikulum (Alokasi JP), Jam Belajar. Tab "Master
// Tingkat" SUDAH TIDAK ditampilkan di halaman ini sejak 2026-08-27 di PHP asli -
// endpoint Tingkat MASIH aktif, dipanggil dari menu Kelola Kelas (route tetap di
// bawah "kurikulum/tingkat" persis arsitektur PHP asli).
[Route("kurikulum")]
public class KurikulumController(DataMasterDbContext db) : Controller
{
    private static readonly string[] Hari = ["senin", "selasa", "rabu", "kamis", "jumat", "sabtu", "minggu"];

    private async Task<int> ResolveTahunAjaranIdAsync(int? tahun)
    {
        if (tahun is > 0) return tahun.Value;
        var aktif = await db.TahunAjaran.FirstOrDefaultAsync(t => t.IsActive);
        return aktif?.TahunAjaranId ?? 0;
    }

    private IActionResult Back(string tab, int ta) => RedirectToAction(nameof(Index), new { tab, tahun_ajaran_id = ta });

    // "kembali_ke" HANYA menerima literal "kelas" (whitelist anti-open-redirect).
    private IActionResult TujuanTingkat(string? kembaliKe, string tab, int ta) =>
        kembaliKe == "kelas" ? RedirectToAction("Index", "Kelas") : Back(tab, ta);

    private IActionResult TujuanMapel(string? kembaliKe, int ta) =>
        kembaliKe == "penugasan-mengajar" ? RedirectToAction("Index", "PenugasanMengajar") : Back("mapel", ta);

    // ---------------------------------------------------------------- Index

    [HttpGet("")]
    public async Task<IActionResult> Index(int? tahun_ajaran_id, string? tab)
    {
        var taId = await ResolveTahunAjaranIdAsync(tahun_ajaran_id);
        var t = (tab is "jam" or "alokasi") ? tab : "mapel";

        var mapelEntities = await db.MataPelajaran.ToListAsync();
        var mapelList = mapelEntities
            .OrderBy(m => m.Urutan == 0) // urutan=0 dilempar ke belakang
            .ThenBy(m => m.Urutan).ThenBy(m => m.Nama)
            .Select(m => new MapelRow { MataPelajaranId = m.MataPelajaranId, Nama = m.Nama, Kode = m.Kode, Kelompok = m.Kelompok, Urutan = m.Urutan })
            .ToList();

        var mapelPerKelompok = mapelList.GroupBy(m => string.IsNullOrEmpty(m.Kelompok) ? "Lainnya" : m.Kelompok)
            .ToDictionary(g => g.Key, g => g.ToList());
        // "Lainnya" SELALU di paling bawah.
        if (mapelPerKelompok.ContainsKey("Lainnya"))
        {
            var lainnya = mapelPerKelompok["Lainnya"];
            mapelPerKelompok.Remove("Lainnya");
            var sorted = mapelPerKelompok.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value);
            sorted["Lainnya"] = lainnya;
            mapelPerKelompok = sorted;
        }
        else
        {
            mapelPerKelompok = mapelPerKelompok.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        var tingkatAktif = await db.Tingkat.Where(x => x.IsActive).OrderBy(x => x.Urutan).ThenBy(x => x.Kode)
            .Select(x => new TingkatOpt { Kode = x.Kode, Nama = x.Nama }).ToListAsync();

        var alokasi = await db.KurikulumAlokasi.Where(a => a.TahunAjaranId == taId).ToListAsync();
        var matriks = new Dictionary<string, Dictionary<int, int>>();
        foreach (var a in alokasi)
        {
            if (!matriks.TryGetValue(a.TingkatKode, out var row)) matriks[a.TingkatKode] = row = [];
            row[a.MataPelajaranId] = a.JpPerMinggu;
        }

        var jam = await db.JamPelajaran.Where(j => j.TahunAjaranId == taId).OrderBy(j => j.JamKe).ToListAsync();
        var jamPerHari = Hari.ToDictionary(h => h, h => jam.Where(j => j.Hari.ToString() == h)
            .Select(j => new JamRow { JamPelajaranId = j.JamPelajaranId, JamKe = j.JamKe, JamMulai = j.JamMulai, JamSelesai = j.JamSelesai, Jenis = j.Jenis.ToString(), Label = j.Label })
            .ToList());

        var vm = new KurikulumIndexViewModel
        {
            TahunAjaranId = taId,
            Tab = t,
            TahunAjaranList = (await db.TahunAjaran.OrderByDescending(x => x.Nama).Select(x => new { x.TahunAjaranId, x.Nama }).ToListAsync())
                .Select(x => (x.TahunAjaranId, x.Nama)).ToList(),
            MapelList = mapelList,
            MapelPerKelompok = mapelPerKelompok,
            TingkatList = tingkatAktif,
            Matriks = matriks,
            JamPerHari = jamPerHari,
        };
        return View(vm);
    }

    // =============================================================== TAB 1: Mata Pelajaran

    [HttpPost("mata-pelajaran/store")]
    public async Task<IActionResult> StoreMapel(int tahun_ajaran_id, string? kode, string? nama, string? kelompok, int urutan, string? kembali_ke)
    {
        var k = string.IsNullOrWhiteSpace(kode) ? null : kode.Trim().ToUpperInvariant();
        var kel = string.IsNullOrWhiteSpace(kelompok) ? null : kelompok.Trim();
        var n = (nama ?? "").Trim();

        var errors = new List<string>();
        if (n == "") errors.Add("Nama mata pelajaran wajib diisi.");
        else if (n.Length < 2) errors.Add("Nama mata pelajaran minimal 2 karakter.");
        else if (n.Length > 100) errors.Add("Nama mata pelajaran maksimal 100 karakter.");
        else if (await db.MataPelajaran.AnyAsync(m => m.Nama == n)) errors.Add("Mata pelajaran ini sudah ada.");
        if (k is not null)
        {
            if (k.Length > 10) errors.Add("Kode maksimal 10 karakter.");
            else if (await db.MataPelajaran.AnyAsync(m => m.Kode == k)) errors.Add("Kode ini sudah dipakai mata pelajaran lain.");
        }

        if (errors.Count > 0)
        {
            TempData["error"] = string.Join(" ", errors);
            return TujuanMapel(kembali_ke, tahun_ajaran_id);
        }

        db.MataPelajaran.Add(new MataPelajaran { Nama = n, Kode = k, Kelompok = kel, Urutan = urutan });
        await db.SaveChangesAsync();
        TempData["message"] = $"Mata pelajaran \"{n}\" ditambahkan.";
        return TujuanMapel(kembali_ke, tahun_ajaran_id);
    }

    public class MapelRowInput
    {
        public string? Nama { get; set; }
        public string? Kode { get; set; }
        public string? Kelompok { get; set; }
        public int Urutan { get; set; }
    }

    [HttpPost("mata-pelajaran/update")]
    public async Task<IActionResult> UpdateMapelBatch(int tahun_ajaran_id, [FromForm] Dictionary<string, MapelRowInput> rows)
    {
        // Dihitung dulu SELURUH state akhir (baris yang diedit + yang tidak) sebelum
        // menyentuh DB - supaya 2 baris yang di batch yang sama diubah jadi nama/kode
        // yang SAMA terdeteksi sebagai bentrok (pesan error rapi), bukan menabrak
        // UNIQUE constraint SQLite mentah-mentah di tengah SaveChanges.
        var semuaMapel = await db.MataPelajaran.ToListAsync();
        var gagal = new List<string>();
        var final = new Dictionary<int, (string Nama, string? Kode, string? Kelompok, int Urutan)>();

        foreach (var m in semuaMapel)
        {
            if (rows.TryGetValue(m.MataPelajaranId.ToString(), out var row))
            {
                var kode = string.IsNullOrWhiteSpace(row.Kode) ? null : row.Kode.Trim().ToUpperInvariant();
                var kelompok = string.IsNullOrWhiteSpace(row.Kelompok) ? null : row.Kelompok.Trim();
                var nama = (row.Nama ?? "").Trim();

                if (nama == "") { gagal.Add($"{m.Nama}: Nama mata pelajaran wajib diisi."); continue; }
                if (nama.Length < 2) { gagal.Add($"{m.Nama}: Nama mata pelajaran minimal 2 karakter."); continue; }

                final[m.MataPelajaranId] = (nama, kode, kelompok, row.Urutan);
            }
            else
            {
                final[m.MataPelajaranId] = (m.Nama, m.Kode, m.Kelompok, m.Urutan);
            }
        }

        // is_unique EXCLUDE baris sendiri - port dari catatan §10 poin C (placeholder
        // mata_pelajaran_id WAJIB) - dicek lintas SELURUH state akhir batch ini.
        var namaGroups = final.GroupBy(kv => kv.Value.Nama).Where(g => g.Count() > 1).SelectMany(g => g.Select(kv => kv.Key)).ToHashSet();
        var kodeGroups = final.Where(kv => kv.Value.Kode is not null).GroupBy(kv => kv.Value.Kode).Where(g => g.Count() > 1).SelectMany(g => g.Select(kv => kv.Key)).ToHashSet();

        var ok = 0;
        foreach (var (idStr, row) in rows)
        {
            if (!int.TryParse(idStr, out var id) || !final.TryGetValue(id, out var f)) continue;
            var m = semuaMapel.FirstOrDefault(x => x.MataPelajaranId == id);
            if (m is null) continue;
            if (namaGroups.Contains(id)) { gagal.Add($"{m.Nama}: Mata pelajaran ini sudah ada."); continue; }
            if (kodeGroups.Contains(id)) { gagal.Add($"{m.Nama}: Kode ini sudah dipakai mata pelajaran lain."); continue; }

            m.Nama = f.Nama;
            m.Kode = f.Kode;
            m.Kelompok = f.Kelompok;
            m.Urutan = f.Urutan;
            ok++;
        }
        await db.SaveChangesAsync();

        if (gagal.Count > 0) TempData["import_errors"] = JsonSerializer.Serialize(gagal);
        TempData["message"] = $"{ok} mata pelajaran diperbarui.";
        return Back("mapel", tahun_ajaran_id);
    }

    [HttpPost("mata-pelajaran/{id:int}/delete")]
    public async Task<IActionResult> DeleteMapel(int id, int tahun_ajaran_id)
    {
        var m = await db.MataPelajaran.FindAsync(id);
        if (m is null)
        {
            TempData["error"] = "Mata pelajaran tidak ditemukan.";
            return Back("mapel", tahun_ajaran_id);
        }
        var dipakai = await db.JadwalPelajaran.AnyAsync(j => j.MataPelajaranId == id);
        if (dipakai)
        {
            TempData["error"] = $"Tidak bisa menghapus \"{m.Nama}\" - masih dipakai di Jadwal Pelajaran. Hapus jadwalnya dulu.";
            return Back("mapel", tahun_ajaran_id);
        }
        db.MataPelajaran.Remove(m);
        await db.SaveChangesAsync();
        TempData["message"] = $"Mata pelajaran \"{m.Nama}\" dihapus.";
        return Back("mapel", tahun_ajaran_id);
    }

    [HttpGet("mata-pelajaran/import")]
    public IActionResult ImportMapel(int? tahun_ajaran_id) { ViewBag.Jenis = "mapel"; ViewBag.TahunAjaranId = tahun_ajaran_id ?? 0; return View("Import"); }

    [HttpGet("mata-pelajaran/template")]
    public IActionResult DownloadTemplateMapel()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Mata Pelajaran");
        var headers = new[] { "Nama", "Kode", "Kelompok", "Urutan" };
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        var rows = new[]
        {
            new[] { "Pendidikan Agama Islam", "A", "Kelompok A (Umum)", "1" },
            new[] { "PJOK", "K", "Kelompok A (Umum)", "2" },
            new[] { "Seni Budaya", "SB", "Kelompok B (Muatan Lokal)", "1" },
        };
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = rows[r][c];
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "template_mata_pelajaran.xlsx");
    }

    [HttpPost("mata-pelajaran/process-import")]
    public async Task<IActionResult> ProcessImportMapel(IFormFile? file, int tahun_ajaran_id)
    {
        var (rows, error) = await BacaExcelImportAsync(file, nameof(ImportMapel), tahun_ajaran_id);
        if (error is not null) return error;

        int masuk = 0, dilewati = 0;
        var errors = new List<string>();
        for (var r = 0; r < rows!.Count; r++)
        {
            var rowNum = r + 2;
            var cells = rows[r];
            var nama = cells.Length > 0 ? cells[0] : "";
            var kode = cells.Length > 1 ? cells[1] : "";
            var kelompok = cells.Length > 2 ? cells[2] : "";
            var urutanRaw = cells.Length > 3 ? cells[3] : "";

            if (nama == "" && kode == "") continue;
            if (nama == "") { errors.Add($"Baris {rowNum}: nama mata pelajaran wajib diisi."); dilewati++; continue; }

            var k = kode == "" ? null : kode.Trim().ToUpperInvariant();
            if (nama.Length < 2 || nama.Length > 100 || await db.MataPelajaran.AnyAsync(m => m.Nama == nama) || (k is not null && (k.Length > 10 || await db.MataPelajaran.AnyAsync(m => m.Kode == k))))
            {
                errors.Add($"Baris {rowNum} ({nama}): mata pelajaran tidak valid atau sudah ada.");
                dilewati++;
                continue;
            }

            int.TryParse(urutanRaw, out var urutan);
            db.MataPelajaran.Add(new MataPelajaran { Nama = nama, Kode = k, Kelompok = kelompok == "" ? null : kelompok, Urutan = urutan });
            // Simpan LANGSUNG per baris - supaya baris berikutnya dalam file yang SAMA
            // ikut kena cek duplikat nama/kode terhadap baris ini juga (bukan cuma
            // data lama di DB) - sama seperti alasan di ProcessImportJam.
            await db.SaveChangesAsync();
            masuk++;
        }

        if (errors.Count > 0) TempData["import_errors"] = JsonSerializer.Serialize(errors);
        TempData["message"] = $"Import selesai: {masuk} mata pelajaran masuk, {dilewati} dilewati.";
        return Back("mapel", tahun_ajaran_id);
    }

    // =============================================================== TAB 2: Alokasi JP

    [HttpPost("alokasi/simpan")]
    public async Task<IActionResult> SimpanAlokasi(int tahun_ajaran_id, [FromForm] Dictionary<string, Dictionary<string, int>> alokasi)
    {
        if (tahun_ajaran_id <= 0)
        {
            TempData["error"] = "Tahun ajaran belum dipilih.";
            return RedirectToAction(nameof(Index));
        }
        // Ditemukan via audit performa (2026-09-08): versi lama query+SaveChanges
        // PER SEL matriks (bisa ratusan sel utk matriks penuh) - sekarang SATU
        // query muat-semua + proses in-memory + SATU SaveChanges di akhir.
        var peta = await MuatPetaAlokasiAsync(tahun_ajaran_id);
        var n = 0;
        foreach (var (tingkatKode, mapelMap) in alokasi)
        {
            foreach (var (mapelIdStr, jp) in mapelMap)
            {
                if (!int.TryParse(mapelIdStr, out var mapelId)) continue;
                SimpanSelInMemory(peta, tahun_ajaran_id, tingkatKode, mapelId, jp);
                n++;
            }
        }
        await db.SaveChangesAsync();
        TempData["message"] = $"Alokasi kurikulum disimpan ({n} sel diperiksa).";
        return Back("alokasi", tahun_ajaran_id);
    }

    private async Task<Dictionary<(string TingkatKode, int MataPelajaranId), KurikulumAlokasi>> MuatPetaAlokasiAsync(int ta) =>
        (await db.KurikulumAlokasi.Where(a => a.TahunAjaranId == ta).ToListAsync()).ToDictionary(a => (a.TingkatKode, a.MataPelajaranId));

    // Versi in-memory dari SimpanSelAsync lama - Remove/Add tetap lewat db (perlu
    // tracking EF), tapi TANPA query/SaveChanges di dalam method ini sendiri;
    // caller (SimpanAlokasi/ProcessImportAlokasi) yang panggil SaveChangesAsync
    // SEKALI di akhir setelah semua sel diproses.
    private void SimpanSelInMemory(Dictionary<(string TingkatKode, int MataPelajaranId), KurikulumAlokasi> peta, int ta, string tingkatKode, int mapelId, int jp)
    {
        var key = (tingkatKode, mapelId);
        if (jp <= 0)
        {
            if (peta.TryGetValue(key, out var existing)) { db.KurikulumAlokasi.Remove(existing); peta.Remove(key); }
        }
        else if (peta.TryGetValue(key, out var existing2))
        {
            existing2.JpPerMinggu = jp;
        }
        else
        {
            var baru = new KurikulumAlokasi { TahunAjaranId = ta, TingkatKode = tingkatKode, MataPelajaranId = mapelId, JpPerMinggu = jp };
            db.KurikulumAlokasi.Add(baru);
            peta[key] = baru;
        }
    }

    [HttpPost("alokasi/salin")]
    public async Task<IActionResult> SalinAlokasi(int tahun_ajaran_id, int sumber_id)
    {
        if (sumber_id <= 0 || sumber_id == tahun_ajaran_id)
        {
            TempData["error"] = "Pilih tahun ajaran sumber yang berbeda.";
            return Back("alokasi", tahun_ajaran_id);
        }
        var sumber = await db.KurikulumAlokasi.Where(a => a.TahunAjaranId == sumber_id).ToListAsync();
        var tujuanSet = (await db.KurikulumAlokasi.Where(a => a.TahunAjaranId == tahun_ajaran_id).Select(a => new { a.TingkatKode, a.MataPelajaranId }).ToListAsync())
            .Select(x => (x.TingkatKode, x.MataPelajaranId)).ToHashSet();

        var n = 0;
        foreach (var a in sumber)
        {
            if (tujuanSet.Contains((a.TingkatKode, a.MataPelajaranId))) continue;
            db.KurikulumAlokasi.Add(new KurikulumAlokasi { TahunAjaranId = tahun_ajaran_id, TingkatKode = a.TingkatKode, MataPelajaranId = a.MataPelajaranId, JpPerMinggu = a.JpPerMinggu });
            n++;
        }
        await db.SaveChangesAsync();
        TempData["message"] = $"{n} baris alokasi disalin. Baris yang sudah ada tidak ditimpa.";
        return Back("alokasi", tahun_ajaran_id);
    }

    [HttpGet("alokasi/import")]
    public IActionResult ImportAlokasi(int? tahun_ajaran_id) { ViewBag.Jenis = "alokasi"; ViewBag.TahunAjaranId = tahun_ajaran_id ?? 0; return View("Import"); }

    [HttpGet("alokasi/template")]
    public async Task<IActionResult> DownloadTemplateAlokasi()
    {
        var contohTingkat = (await db.Tingkat.Where(t => t.IsActive).OrderBy(t => t.Urutan).FirstOrDefaultAsync())?.Kode ?? "1";
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Struktur Kurikulum");
        var headers = new[] { "Tingkat", "Mata Pelajaran", "JP per Minggu" };
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Cell(2, 1).Value = contohTingkat; ws.Cell(2, 2).Value = "Pendidikan Agama Islam"; ws.Cell(2, 3).Value = "3";

        var panduan = wb.Worksheets.Add("Panduan");
        var daftarKode = string.Join(", ", await db.Tingkat.Where(t => t.IsActive).OrderBy(t => t.Urutan).Select(t => t.Kode).ToListAsync());
        panduan.Cell(1, 1).Value = $@"CARA MENGISI

Tingkat        : kode tingkat SESUAI yang sudah ada di menu Kelola Kelas ({daftarKode}).
Mata Pelajaran : nama PERSIS sesuai tab Mata Pelajaran (tambahkan/import dulu di sana kalau belum ada).
JP per Minggu  : angka. Isi 0 atau kosongkan berarti mapel itu TIDAK diajarkan di tingkat itu (baris dilewati, bukan error).

Baris dgn tingkat/mapel yang tidak dikenali akan DILEWATI dan dilaporkan.";
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "template_struktur_kurikulum.xlsx");
    }

    [HttpPost("alokasi/process-import")]
    public async Task<IActionResult> ProcessImportAlokasi(IFormFile? file, int tahun_ajaran_id)
    {
        var (rows, error) = await BacaExcelImportAsync(file, nameof(ImportAlokasi), tahun_ajaran_id);
        if (error is not null) return error;
        if (tahun_ajaran_id <= 0)
        {
            TempData["error"] = "Tahun ajaran belum dipilih.";
            return RedirectToAction(nameof(Index));
        }

        var tingkatValid = (await db.Tingkat.Where(t => t.IsActive).Select(t => t.Kode).ToListAsync()).ToHashSet();
        var mapelByNama = await db.MataPelajaran.ToDictionaryAsync(m => m.Nama.ToLowerInvariant(), m => m.MataPelajaranId);
        var peta = await MuatPetaAlokasiAsync(tahun_ajaran_id);

        int masuk = 0, dilewati = 0;
        var errors = new List<string>();
        for (var r = 0; r < rows!.Count; r++)
        {
            var rowNum = r + 2;
            var cells = rows[r];
            var tingkat = cells.Length > 0 ? cells[0] : "";
            var namaMapel = cells.Length > 1 ? cells[1] : "";
            var jpRaw = cells.Length > 2 ? cells[2] : "";

            if (tingkat == "" && namaMapel == "") continue;
            if (!tingkatValid.Contains(tingkat)) { errors.Add($"Baris {rowNum}: tingkat \"{tingkat}\" tidak dikenali (cek Kelola Kelas)."); dilewati++; continue; }
            if (!mapelByNama.TryGetValue(namaMapel.ToLowerInvariant(), out var mapelId)) { errors.Add($"Baris {rowNum}: mata pelajaran \"{namaMapel}\" tidak ditemukan (tambahkan dulu di tab Mata Pelajaran)."); dilewati++; continue; }

            int.TryParse(jpRaw, out var jp);
            SimpanSelInMemory(peta, tahun_ajaran_id, tingkat, mapelId, jp);
            masuk++;
        }
        await db.SaveChangesAsync();

        if (errors.Count > 0) TempData["import_errors"] = JsonSerializer.Serialize(errors);
        TempData["message"] = $"Import selesai: {masuk} sel diproses, {dilewati} dilewati.";
        return Back("alokasi", tahun_ajaran_id);
    }

    // =============================================================== TAB 3: Jam Belajar

    private static bool HariValid(string h) => Hari.Contains(h);

    private async Task<Data.Entities.JamPelajaran?> CekBentrokNomorJamAsync(int ta, Hari hariEnum, int jamKe, int? excludeId = null)
    {
        var q = db.JamPelajaran.Where(j => j.TahunAjaranId == ta && j.Hari == hariEnum && j.JamKe == jamKe);
        if (excludeId is not null) q = q.Where(j => j.JamPelajaranId != excludeId);
        return await q.FirstOrDefaultAsync();
    }

    private async Task<Data.Entities.JamPelajaran?> CekTumpangTindihJamAsync(int ta, Hari hariEnum, TimeOnly mulai, TimeOnly selesai, int? excludeId = null)
    {
        var q = db.JamPelajaran.Where(j => j.TahunAjaranId == ta && j.Hari == hariEnum && j.JamMulai < selesai && j.JamSelesai > mulai);
        if (excludeId is not null) q = q.Where(j => j.JamPelajaranId != excludeId);
        return await q.FirstOrDefaultAsync();
    }

    [HttpPost("jam/store")]
    public async Task<IActionResult> StoreJam(int tahun_ajaran_id, string? hari, string? jenis, int jam_ke, TimeOnly? jam_mulai, TimeOnly? jam_selesai, string? label)
    {
        var ajax = Request.Headers.XRequestedWith == "XMLHttpRequest";
        IActionResult Gagal(string pesan)
        {
            if (ajax) return StatusCode(422, new { success = false, message = pesan });
            TempData["error"] = pesan;
            return Back("jam", tahun_ajaran_id);
        }

        if (tahun_ajaran_id <= 0 || string.IsNullOrEmpty(hari) || !HariValid(hari) || jam_mulai is null || jam_selesai is null)
            return Gagal("Hari, jam mulai, dan jam selesai wajib diisi.");
        if (jam_mulai >= jam_selesai)
            return Gagal("Jam selesai harus setelah jam mulai.");

        var hariEnum = Enum.Parse<Hari>(hari);
        var jenisVal = jenis == "kegiatan" ? JenisJamPelajaran.kegiatan : JenisJamPelajaran.pelajaran;
        var labelVal = jenisVal == JenisJamPelajaran.kegiatan ? (string.IsNullOrWhiteSpace(label) ? "Kegiatan" : label.Trim()) : null;

        if (jam_ke <= 0)
        {
            var maxJamKe = await db.JamPelajaran.Where(j => j.TahunAjaranId == tahun_ajaran_id && j.Hari == hariEnum).Select(j => (int?)j.JamKe).MaxAsync() ?? 0;
            jam_ke = maxJamKe + 1;
        }

        var bentrokNomor = await CekBentrokNomorJamAsync(tahun_ajaran_id, hariEnum, jam_ke);
        if (bentrokNomor is not null) return Gagal($"Jam ke-{jam_ke} pada hari {hari} sudah ada.");

        var bentrokWaktu = await CekTumpangTindihJamAsync(tahun_ajaran_id, hariEnum, jam_mulai.Value, jam_selesai.Value);
        if (bentrokWaktu is not null)
        {
            var labelBentrok = bentrokWaktu.Jenis == JenisJamPelajaran.kegiatan ? (bentrokWaktu.Label ?? "Kegiatan") : "jam pelajaran";
            var waktuBentrok = $"{bentrokWaktu.JamMulai:HH:mm}-{bentrokWaktu.JamSelesai:HH:mm}";
            return Gagal($"Waktu {jam_mulai:HH:mm}-{jam_selesai:HH:mm} bentrok dengan \"{labelBentrok}\" ({waktuBentrok}) pada hari {hari}.");
        }

        var jamBaru = new Data.Entities.JamPelajaran { TahunAjaranId = tahun_ajaran_id, Hari = hariEnum, JamKe = jam_ke, JamMulai = jam_mulai.Value, JamSelesai = jam_selesai.Value, Jenis = jenisVal, Label = labelVal };
        db.JamPelajaran.Add(jamBaru);
        await db.SaveChangesAsync();

        if (ajax) return Json(new { success = true, jam = new { jamBaru.JamPelajaranId, hari, jamBaru.JamKe, jamMulai = jamBaru.JamMulai.ToString("HH:mm"), jamSelesai = jamBaru.JamSelesai.ToString("HH:mm"), jenis = jamBaru.Jenis.ToString(), jamBaru.Label } });
        TempData["message"] = $"Jam ke-{jam_ke} hari {hari} ditambahkan.";
        return Back("jam", tahun_ajaran_id);
    }

    public class JamRowInput
    {
        public TimeOnly? JamMulai { get; set; }
        public TimeOnly? JamSelesai { get; set; }
        public string? Jenis { get; set; }
        public string? Label { get; set; }
    }

    [HttpPost("jam/update")]
    public async Task<IActionResult> UpdateJamBatch(int tahun_ajaran_id, [FromForm] Dictionary<string, JamRowInput> rows)
    {
        var ok = 0;
        foreach (var (idStr, row) in rows)
        {
            if (!int.TryParse(idStr, out var id)) continue;
            var j = await db.JamPelajaran.FindAsync(id);
            if (j is null) continue;
            // CATATAN KRUSIAL: batch update ini TIDAK mengecek ulang bentrok nomor/
            // tumpang tindih - hanya jam_mulai<jam_selesai. INKONSISTENSI NYATA vs
            // storeJam/processImportJam, WAJIB direplikasi apa adanya - lihat §10 poin D.
            if (row.JamMulai is null || row.JamSelesai is null || row.JamMulai >= row.JamSelesai) continue;
            j.JamMulai = row.JamMulai.Value;
            j.JamSelesai = row.JamSelesai.Value;
            j.Jenis = row.Jenis == "kegiatan" ? JenisJamPelajaran.kegiatan : JenisJamPelajaran.pelajaran;
            j.Label = j.Jenis == JenisJamPelajaran.kegiatan ? (string.IsNullOrWhiteSpace(row.Label) ? "Kegiatan" : row.Label.Trim()) : null;
            ok++;
        }
        await db.SaveChangesAsync();
        TempData["message"] = $"{ok} baris jam belajar diperbarui.";
        return Back("jam", tahun_ajaran_id);
    }

    [HttpPost("jam/{id:int}/delete")]
    public async Task<IActionResult> DeleteJam(int id, int tahun_ajaran_id)
    {
        var j = await db.JamPelajaran.FindAsync(id);
        if (j is null)
        {
            TempData["error"] = "Jam pelajaran tidak ditemukan.";
            return Back("jam", tahun_ajaran_id);
        }
        var terpakai = await db.JadwalPelajaran.CountAsync(jp => jp.JamPelajaranId == id);
        var jamKe = j.JamKe;
        var hari = j.Hari.ToString();
        db.JamPelajaran.Remove(j); // FK CASCADE ke jadwal_pelajaran
        await db.SaveChangesAsync();

        var msg = $"Jam ke-{jamKe} hari {hari} dihapus.";
        if (terpakai > 0) msg += $" {terpakai} isian jadwal pada jam tersebut ikut terhapus.";
        TempData["message"] = msg;
        return Back("jam", tahun_ajaran_id);
    }

    [HttpPost("jam/salin")]
    public async Task<IActionResult> SalinJam(int tahun_ajaran_id, int sumber_id)
    {
        if (sumber_id <= 0 || sumber_id == tahun_ajaran_id)
        {
            TempData["error"] = "Pilih tahun ajaran sumber yang berbeda.";
            return Back("jam", tahun_ajaran_id);
        }
        var sumber = await db.JamPelajaran.Where(j => j.TahunAjaranId == sumber_id).ToListAsync();
        var tujuanSet = (await db.JamPelajaran.Where(j => j.TahunAjaranId == tahun_ajaran_id).Select(j => new { j.Hari, j.JamKe }).ToListAsync())
            .Select(x => (x.Hari, x.JamKe)).ToHashSet();

        var n = 0;
        foreach (var j in sumber)
        {
            if (tujuanSet.Contains((j.Hari, j.JamKe))) continue;
            db.JamPelajaran.Add(new Data.Entities.JamPelajaran { TahunAjaranId = tahun_ajaran_id, Hari = j.Hari, JamKe = j.JamKe, JamMulai = j.JamMulai, JamSelesai = j.JamSelesai, Jenis = j.Jenis, Label = j.Label });
            n++;
        }
        await db.SaveChangesAsync();
        TempData["message"] = $"{n} baris jam belajar disalin.";
        return Back("jam", tahun_ajaran_id);
    }

    [HttpGet("jam/import")]
    public IActionResult ImportJam(int? tahun_ajaran_id) { ViewBag.Jenis = "jam"; ViewBag.TahunAjaranId = tahun_ajaran_id ?? 0; return View("Import"); }

    [HttpGet("jam/template")]
    public IActionResult DownloadTemplateJam()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Jam Belajar");
        var headers = new[] { "Hari", "Jam Ke", "Jenis", "Jam Mulai", "Jam Selesai", "Label" };
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Cell(2, 1).Value = "senin"; ws.Cell(2, 2).Value = ""; ws.Cell(2, 3).Value = "pelajaran"; ws.Cell(2, 4).Value = "07:30"; ws.Cell(2, 5).Value = "08:10"; ws.Cell(2, 6).Value = "";
        ws.Cell(3, 1).Value = "senin"; ws.Cell(3, 2).Value = ""; ws.Cell(3, 3).Value = "kegiatan"; ws.Cell(3, 4).Value = "09:20"; ws.Cell(3, 5).Value = "09:40"; ws.Cell(3, 6).Value = "Istirahat";

        var panduan = wb.Worksheets.Add("Panduan");
        panduan.Cell(1, 1).Value = @"CARA MENGISI

Hari       : senin/selasa/rabu/kamis/jumat/sabtu/minggu (huruf kecil).
Jam Ke     : boleh dikosongkan - otomatis lanjutan terakhir hari itu.
Jenis      : ""pelajaran"" atau ""kegiatan"" (mis. Upacara/Istirahat).
Jam Mulai/Selesai : format 24 jam ""HH:MM"", mis. 07:30.
Label      : wajib diisi kalau Jenis = kegiatan (nama kegiatan), dikosongkan kalau pelajaran.

Baris yang jamnya BENTROK (nomor jam ke- ATAU rentang waktu tumpang tindih dgn baris lain di hari yang sama) akan DILEWATI dan dilaporkan - bukan menimpa data yang sudah ada.";
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "template_jam_belajar.xlsx");
    }

    [HttpPost("jam/process-import")]
    public async Task<IActionResult> ProcessImportJam(IFormFile? file, int tahun_ajaran_id)
    {
        var (rows, error) = await BacaExcelImportAsync(file, nameof(ImportJam), tahun_ajaran_id);
        if (error is not null) return error;
        if (tahun_ajaran_id <= 0)
        {
            TempData["error"] = "Tahun ajaran belum dipilih.";
            return RedirectToAction(nameof(Index));
        }

        int masuk = 0, dilewati = 0;
        var errors = new List<string>();
        for (var r = 0; r < rows!.Count; r++)
        {
            var rowNum = r + 2;
            var cells = rows[r];
            var hariRaw = (cells.Length > 0 ? cells[0] : "").Trim().ToLowerInvariant();
            var jamKeRaw = cells.Length > 1 ? cells[1] : "";
            var jenisRaw = (cells.Length > 2 ? cells[2] : "").Trim().ToLowerInvariant();
            var mulaiRaw = cells.Length > 3 ? cells[3] : "";
            var selesaiRaw = cells.Length > 4 ? cells[4] : "";
            var labelRaw = cells.Length > 5 ? cells[5] : "";

            if (hariRaw == "") continue;
            if (!HariValid(hariRaw)) { errors.Add($"Baris {rowNum}: hari \"{hariRaw}\" tidak dikenali."); dilewati++; continue; }

            var jenis = jenisRaw == "kegiatan" ? JenisJamPelajaran.kegiatan : JenisJamPelajaran.pelajaran;
            var label = jenis == JenisJamPelajaran.kegiatan ? (string.IsNullOrWhiteSpace(labelRaw) ? "Kegiatan" : labelRaw.Trim()) : null;

            if (!Regex.IsMatch(mulaiRaw, @"^\d{1,2}:\d{2}$") || !Regex.IsMatch(selesaiRaw, @"^\d{1,2}:\d{2}$"))
            {
                errors.Add($"Baris {rowNum}: jam mulai/selesai harus format HH:MM (mis. 07:30).");
                dilewati++; continue;
            }
            var mulai = TimeOnly.Parse(mulaiRaw);
            var selesai = TimeOnly.Parse(selesaiRaw);
            if (mulai >= selesai) { errors.Add($"Baris {rowNum}: jam selesai harus setelah jam mulai."); dilewati++; continue; }

            var hariEnum = Enum.Parse<Hari>(hariRaw);
            int jamKe;
            if (int.TryParse(jamKeRaw, out var jk) && jk > 0)
            {
                jamKe = jk;
            }
            else
            {
                var maxJamKe = await db.JamPelajaran.Where(j => j.TahunAjaranId == tahun_ajaran_id && j.Hari == hariEnum).Select(j => (int?)j.JamKe).MaxAsync() ?? 0;
                jamKe = maxJamKe + 1;
            }

            var bentrokNomor = await CekBentrokNomorJamAsync(tahun_ajaran_id, hariEnum, jamKe);
            if (bentrokNomor is not null) { errors.Add($"Baris {rowNum}: jam ke-{jamKe} pada hari {hariRaw} sudah ada."); dilewati++; continue; }

            var bentrokWaktu = await CekTumpangTindihJamAsync(tahun_ajaran_id, hariEnum, mulai, selesai);
            if (bentrokWaktu is not null)
            {
                var labelBentrok = bentrokWaktu.Jenis == JenisJamPelajaran.kegiatan ? (bentrokWaktu.Label ?? "Kegiatan") : "jam pelajaran";
                var waktuBentrok = $"{bentrokWaktu.JamMulai:HH:mm}-{bentrokWaktu.JamSelesai:HH:mm}";
                errors.Add($"Baris {rowNum}: waktu {mulaiRaw}-{selesaiRaw} bentrok dengan \"{labelBentrok}\" ({waktuBentrok}) pada hari {hariRaw}.");
                dilewati++; continue;
            }

            db.JamPelajaran.Add(new Data.Entities.JamPelajaran { TahunAjaranId = tahun_ajaran_id, Hari = hariEnum, JamKe = jamKe, JamMulai = mulai, JamSelesai = selesai, Jenis = jenis, Label = label });
            // Simpan LANGSUNG per baris (bukan 1 SaveChanges di akhir) - supaya baris
            // berikutnya dalam file yang SAMA ikut kena cek bentrok nomor/tumpang
            // tindih terhadap baris ini juga, bukan cuma terhadap data lama di DB.
            await db.SaveChangesAsync();
            masuk++;
        }

        if (errors.Count > 0) TempData["import_errors"] = JsonSerializer.Serialize(errors);
        TempData["message"] = $"Import selesai: {masuk} baris jam masuk, {dilewati} dilewati.";
        return Back("jam", tahun_ajaran_id);
    }

    // ============================================================ TAB 4: Master Tingkat

    [HttpPost("tingkat/store")]
    public async Task<IActionResult> StoreTingkat(string? kode, string? nama, int urutan, string? kembali_ke, int tahun_ajaran_id = 0)
    {
        var k = (kode ?? "").Trim();
        var n = (nama ?? "").Trim();
        var errors = new List<string>();
        if (k == "") errors.Add("Kode tingkat wajib diisi.");
        else if (k.Length > 20) errors.Add("Kode tingkat maksimal 20 karakter.");
        else if (await db.Tingkat.AnyAsync(t => t.Kode == k)) errors.Add("Kode tingkat ini sudah dipakai.");
        if (n == "") errors.Add("Nama tingkat wajib diisi.");
        else if (n.Length > 50) errors.Add("Nama tingkat maksimal 50 karakter.");

        if (errors.Count > 0)
        {
            TempData["error"] = string.Join(" ", errors);
            return TujuanTingkat(kembali_ke, "mapel", tahun_ajaran_id);
        }

        db.Tingkat.Add(new Tingkat { Kode = k, Nama = n, Urutan = urutan, IsActive = true });
        await db.SaveChangesAsync();
        TempData["message"] = $"Tingkat \"{n}\" ditambahkan.";
        return TujuanTingkat(kembali_ke, "mapel", tahun_ajaran_id);
    }

    [HttpPost("tingkat/update")]
    public async Task<IActionResult> UpdateTingkatBatch([FromForm] Dictionary<string, TingkatRowInput> rows, [FromForm(Name = "is_active")] List<int>? isActive, string? kembali_ke, int tahun_ajaran_id = 0)
    {
        var aktifSet = (isActive ?? []).ToHashSet();
        var ok = 0;
        foreach (var (idStr, row) in rows)
        {
            if (!int.TryParse(idStr, out var id)) continue;
            var t = await db.Tingkat.FindAsync(id);
            if (t is null) continue;
            t.Nama = (row.Nama ?? t.Nama).Trim();
            t.Urutan = row.Urutan;
            t.IsActive = aktifSet.Contains(id);
            ok++;
        }
        await db.SaveChangesAsync();
        TempData["message"] = $"{ok} tingkat diperbarui.";
        return TujuanTingkat(kembali_ke, "mapel", tahun_ajaran_id);
    }

    [HttpPost("tingkat/{id:int}/delete")]
    public async Task<IActionResult> DeleteTingkat(int id, string? kembali_ke, int tahun_ajaran_id = 0)
    {
        var t = await db.Tingkat.FindAsync(id);
        if (t is null)
        {
            TempData["error"] = "Tingkat tidak ditemukan.";
            return TujuanTingkat(kembali_ke, "mapel", tahun_ajaran_id);
        }
        var dipakai = await db.Kelas.CountAsync(k => k.Tingkat == t.Kode);
        if (dipakai > 0)
        {
            TempData["error"] = $"Tidak bisa menghapus \"{t.Nama}\" - masih dipakai {dipakai} kelas. Nonaktifkan saja bila sudah tidak dipakai.";
            return TujuanTingkat(kembali_ke, "mapel", tahun_ajaran_id);
        }
        db.Tingkat.Remove(t);
        await db.SaveChangesAsync();
        TempData["message"] = $"Tingkat \"{t.Nama}\" dihapus.";
        return TujuanTingkat(kembali_ke, "mapel", tahun_ajaran_id);
    }

    // ---------------------------------------------------------------- Helper

    private async Task<(List<string[]>? Rows, IActionResult? Error)> BacaExcelImportAsync(IFormFile? file, string importAction, int tahunAjaranId)
    {
        if (file is null || file.Length == 0) return (null, RedirectWithError("File tidak ditemukan.", importAction, tahunAjaranId));
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls") return (null, RedirectWithError("Format file harus .xlsx atau .xls.", importAction, tahunAjaranId));
        if (file.Length > 5 * 1024 * 1024) return (null, RedirectWithError("Ukuran file terlalu besar. Maksimal 5 MB.", importAction, tahunAjaranId));

        try
        {
            using var stream = file.OpenReadStream();
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();
            var allRows = ws.RowsUsed().Select(row => Enumerable.Range(1, 10).Select(i => row.Cell(i).GetString().Trim()).ToArray()).ToList();
            if (allRows.Count == 0) return ([], null);
            return (allRows.Skip(1).ToList(), null); // buang header
        }
        catch (Exception ex)
        {
            return (null, RedirectWithError($"Gagal membaca file Excel: {ex.Message}", importAction, tahunAjaranId));
        }
    }

    private IActionResult RedirectWithError(string pesan, string action, int tahunAjaranId)
    {
        TempData["error"] = pesan;
        return RedirectToAction(action, new { tahun_ajaran_id = tahunAjaranId });
    }
}

public class TingkatRowInput
{
    public string? Nama { get; set; }
    public int Urutan { get; set; }
}
