using Microsoft.AspNetCore.Authorization;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DataMaster.Data;
using DataMaster.Data.Entities;
using DataMaster.Web.Models.Siswa;
using DataMaster.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Controllers;

// Port 1:1 dari app/Controllers/Siswa.php - lihat spec/01-siswa-psb.md untuk
// rujukan lengkap tiap aturan/pesan/edge-case. Jangan ubah pesan/urutan validasi
// tanpa mengecek ulang spec tsb - banyak teks di sini SENGAJA verbatim (termasuk
// yang terasa tidak konsisten, mis. "Hapus Data" yang sebenarnya cuma arsip).
[Authorize(Roles = "admin")]
[Route("siswa")]
public class SiswaController(DataMasterDbContext db, DocumentStorageService docs) : Controller
{
    private static readonly int[] AllowedPerPage = [50, 100, 150, 200];
    private static readonly string[] ExpectedHeaders =
        ["NAMA", "JK", "NIS", "NISN", "TTL", "ASAL TK", "JALAN", "RT", "RW", "KEL", "KEC", "AYAH", "IBU", "PEKERJAAN AYAH", "PEKERJAAN IBU", "NO HP"];

    private const string PreviewSessionKey = "preview_import_siswa";

    // ---------------------------------------------------------------- Index

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, int? per_page, int? page, string? ajax)
    {
        var keyword = (q ?? "").Trim();
        var perPage = per_page.HasValue && AllowedPerPage.Contains(per_page.Value) ? per_page.Value : 50;
        var pageNum = Math.Max(1, page ?? 1);

        var model = new SiswaIndexViewModel { Keyword = keyword, PerPage = perPage };

        if (keyword != "")
        {
            var semua = await db.Siswa.Include(s => s.Kelas)
                .Where(s => s.Status == StatusSiswa.aktif)
                .OrderBy(s => s.Nama)
                .ToListAsync();
            var hasil = semua.Where(s => TextSearchService.MatchesAny(keyword, s.Nama, s.Nis)).ToList();
            model.Rows = hasil.Select(ToRow).ToList();
            model.TotalSiswa = hasil.Count;
            model.TotalL = hasil.Count(s => s.JenisKelamin == JenisKelamin.L);
            model.TotalP = hasil.Count(s => s.JenisKelamin == JenisKelamin.P);
            model.Page = 1;
            model.TotalPages = 1;
        }
        else
        {
            var aktifQuery = db.Siswa.Where(s => s.Status == StatusSiswa.aktif);
            model.TotalSiswa = await aktifQuery.CountAsync();
            model.TotalL = await aktifQuery.CountAsync(s => s.JenisKelamin == JenisKelamin.L);
            model.TotalP = await aktifQuery.CountAsync(s => s.JenisKelamin == JenisKelamin.P);
            model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalSiswa / (double)perPage));
            model.Page = Math.Min(pageNum, model.TotalPages);
            var offset = (model.Page - 1) * perPage;
            var rows = await db.Siswa.Include(s => s.Kelas)
                .Where(s => s.Status == StatusSiswa.aktif)
                .OrderBy(s => s.Nama)
                .Skip(offset).Take(perPage)
                .ToListAsync();
            model.Rows = rows.Select(ToRow).ToList();
        }

        if (TempData["import_errors"] is string errJson)
        {
            model.ImportErrors = JsonSerializer.Deserialize<List<string>>(errJson) ?? [];
        }

        if (ajax == "1")
        {
            return PartialView("_Hasil", model);
        }
        return View(model);
    }

    private static SiswaRow ToRow(Siswa s) => new()
    {
        SiswaId = s.SiswaId,
        Nama = s.Nama,
        Nis = s.Nis,
        Nisn = s.Nisn,
        JenisKelamin = s.JenisKelamin.ToString(),
        NamaKelas = s.Kelas?.NamaKelas,
        TempatLahir = s.TempatLahir,
        TanggalLahir = s.TanggalLahir,
        Status = s.Status.ToString(),
    };

    // --------------------------------------------------------------- Create

    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        return View(new SiswaFormViewModel { KelasOptions = await GetKelasOptionsAsync() });
    }

    [HttpPost("store")]
    public async Task<IActionResult> Store(SiswaFormInput input)
    {
        var errors = await ValidateFormAsync(input, excludeId: null);
        if (errors.Count > 0)
        {
            return View("Create", new SiswaFormViewModel { Input = input, Errors = errors, KelasOptions = await GetKelasOptionsAsync() });
        }

        var siswa = new Siswa
        {
            Nama = input.Nama.Trim(),
            JenisKelamin = Enum.Parse<JenisKelamin>(input.JenisKelamin),
            Nis = input.Nis.Trim(),
            Nisn = NullIfEmpty(input.Nisn),
            KelasId = input.KelasId,
            Status = StatusSiswa.aktif,
            TempatLahir = NullIfEmpty(input.TempatLahir),
            TanggalLahir = input.TanggalLahir,
            AsalSekolah = NullIfEmpty(input.AsalSekolah),
            AlamatJalan = NullIfEmpty(input.AlamatJalan),
            AlamatRt = NullIfEmpty(input.AlamatRt),
            AlamatRw = NullIfEmpty(input.AlamatRw),
            AlamatKelurahan = NullIfEmpty(input.AlamatKelurahan),
            AlamatKecamatan = NullIfEmpty(input.AlamatKecamatan),
            NamaAyah = NullIfEmpty(input.NamaAyah),
            PekerjaanAyah = NullIfEmpty(input.PekerjaanAyah),
            NamaIbu = NullIfEmpty(input.NamaIbu),
            PekerjaanIbu = NullIfEmpty(input.PekerjaanIbu),
            NoHandphone = OnlyDigits(input.NoHandphone),
        };

        if (input.DokumenKk is { Length: > 0 } f1) siswa.DokumenKk = await docs.SaveAsync(f1);
        if (input.DokumenAkta is { Length: > 0 } f2) siswa.DokumenAkta = await docs.SaveAsync(f2);
        if (input.DokumenKia is { Length: > 0 } f3) siswa.DokumenKia = await docs.SaveAsync(f3);
        if (input.DokumenIjazah is { Length: > 0 } f4) siswa.DokumenIjazah = await docs.SaveAsync(f4);

        db.Siswa.Add(siswa);
        await db.SaveChangesAsync();

        TempData["message"] = "Data siswa berhasil ditambahkan.";
        return RedirectToAction(nameof(Index));
    }

    // --------------------------------------------------------------- Detail

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var siswa = await db.Siswa.FindAsync(id);
        if (siswa is null) return NotFound($"Siswa dengan ID {id} tidak ditemukan.");

        var vm = ToFormViewModel(siswa);
        vm.KelasOptions = await GetKelasOptionsAsync();
        return View(vm);
    }

    private static SiswaFormViewModel ToFormViewModel(Siswa siswa) => new()
    {
        Input = new SiswaFormInput
        {
            SiswaId = siswa.SiswaId,
            Nama = siswa.Nama,
            JenisKelamin = siswa.JenisKelamin.ToString(),
            Nis = siswa.Nis,
            Nisn = siswa.Nisn,
            KelasId = siswa.KelasId,
            TempatLahir = siswa.TempatLahir,
            TanggalLahir = siswa.TanggalLahir,
            AsalSekolah = siswa.AsalSekolah,
            AlamatJalan = siswa.AlamatJalan,
            AlamatRt = siswa.AlamatRt,
            AlamatRw = siswa.AlamatRw,
            AlamatKelurahan = siswa.AlamatKelurahan,
            AlamatKecamatan = siswa.AlamatKecamatan,
            NamaAyah = siswa.NamaAyah,
            PekerjaanAyah = siswa.PekerjaanAyah,
            NamaIbu = siswa.NamaIbu,
            PekerjaanIbu = siswa.PekerjaanIbu,
            NoHandphone = siswa.NoHandphone,
        },
        DokumenKkLama = siswa.DokumenKk,
        DokumenAktaLama = siswa.DokumenAkta,
        DokumenKiaLama = siswa.DokumenKia,
        DokumenIjazahLama = siswa.DokumenIjazah,
        CreatedAt = siswa.CreatedAt,
    };

    [HttpPost("{id:int}/update")]
    public async Task<IActionResult> Update(int id, SiswaFormInput input)
    {
        var siswa = await db.Siswa.FindAsync(id);
        if (siswa is null) return NotFound($"Siswa dengan ID {id} tidak ditemukan.");

        var errors = await ValidateFormAsync(input, excludeId: id);
        if (errors.Count > 0)
        {
            var vm = ToFormViewModel(siswa);
            vm.Input = input;
            vm.Errors = errors;
            vm.KelasOptions = await GetKelasOptionsAsync();
            return View("Detail", vm);
        }

        siswa.Nama = input.Nama.Trim();
        siswa.JenisKelamin = Enum.Parse<JenisKelamin>(input.JenisKelamin);
        siswa.Nis = input.Nis.Trim();
        siswa.Nisn = NullIfEmpty(input.Nisn);
        siswa.KelasId = input.KelasId;
        siswa.TempatLahir = NullIfEmpty(input.TempatLahir);
        siswa.TanggalLahir = input.TanggalLahir;
        siswa.AsalSekolah = NullIfEmpty(input.AsalSekolah);
        siswa.AlamatJalan = NullIfEmpty(input.AlamatJalan);
        siswa.AlamatRt = NullIfEmpty(input.AlamatRt);
        siswa.AlamatRw = NullIfEmpty(input.AlamatRw);
        siswa.AlamatKelurahan = NullIfEmpty(input.AlamatKelurahan);
        siswa.AlamatKecamatan = NullIfEmpty(input.AlamatKecamatan);
        siswa.NamaAyah = NullIfEmpty(input.NamaAyah);
        siswa.PekerjaanAyah = NullIfEmpty(input.PekerjaanAyah);
        siswa.NamaIbu = NullIfEmpty(input.NamaIbu);
        siswa.PekerjaanIbu = NullIfEmpty(input.PekerjaanIbu);
        siswa.NoHandphone = OnlyDigits(input.NoHandphone);

        // File baru diupload -> hapus dulu file lama, baru set nama baru. Tidak
        // ada file baru -> kolom TIDAK disentuh sama sekali (nilai lama dipertahankan).
        if (input.DokumenKk is { Length: > 0 } f1) { docs.Delete(siswa.DokumenKk); siswa.DokumenKk = await docs.SaveAsync(f1); }
        if (input.DokumenAkta is { Length: > 0 } f2) { docs.Delete(siswa.DokumenAkta); siswa.DokumenAkta = await docs.SaveAsync(f2); }
        if (input.DokumenKia is { Length: > 0 } f3) { docs.Delete(siswa.DokumenKia); siswa.DokumenKia = await docs.SaveAsync(f3); }
        if (input.DokumenIjazah is { Length: > 0 } f4) { docs.Delete(siswa.DokumenIjazah); siswa.DokumenIjazah = await docs.SaveAsync(f4); }

        await db.SaveChangesAsync();
        TempData["message"] = "Data siswa berhasil diperbarui.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ Dokumen

    private static readonly Dictionary<string, string> DokumenLabels = new()
    {
        ["DokumenKk"] = "KK",
        ["DokumenAkta"] = "Akta",
        ["DokumenKia"] = "KIA",
        ["DokumenIjazah"] = "Ijazah",
    };

    [HttpGet("{id:int}/dokumen/{field}")]
    public async Task<IActionResult> Dokumen(int id, string field)
    {
        if (!DokumenLabels.TryGetValue(field, out var label)) return NotFound("Dokumen tidak ditemukan.");
        var siswa = await db.Siswa.FindAsync(id);
        var namaFile = siswa is null ? null : GetDokumenValue(siswa, field);
        var fullPath = docs.GetFullPath(namaFile);
        if (siswa is null || fullPath is null) return NotFound("Dokumen tidak ditemukan.");

        var nisAman = Regex.Replace(siswa.Nis, "[^A-Za-z0-9_-]+", "-");
        var ext = Path.GetExtension(fullPath);
        Response.Headers.CacheControl = "private, no-store, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.ContentDisposition = $"inline; filename=\"{label}-{nisAman}{ext}\"";
        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
        return File(bytes, DocumentStorageService.GetContentType(fullPath));
    }

    private static string? GetDokumenValue(Siswa s, string field) => field switch
    {
        "DokumenKk" => s.DokumenKk,
        "DokumenAkta" => s.DokumenAkta,
        "DokumenKia" => s.DokumenKia,
        "DokumenIjazah" => s.DokumenIjazah,
        _ => null,
    };

    private static void SetDokumenValue(Siswa s, string field, string? value)
    {
        switch (field)
        {
            case "DokumenKk": s.DokumenKk = value; break;
            case "DokumenAkta": s.DokumenAkta = value; break;
            case "DokumenKia": s.DokumenKia = value; break;
            case "DokumenIjazah": s.DokumenIjazah = value; break;
        }
    }

    [HttpPost("{id:int}/delete-dokumen/{field}")]
    public async Task<IActionResult> DeleteDokumen(int id, string field)
    {
        if (!DokumenLabels.ContainsKey(field))
        {
            TempData["error"] = "Bidang dokumen tidak valid.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        var siswa = await db.Siswa.FindAsync(id);
        if (siswa is null) return NotFound($"Siswa dengan ID {id} tidak ditemukan.");

        var current = GetDokumenValue(siswa, field);
        if (string.IsNullOrEmpty(current))
        {
            TempData["error"] = "Dokumen tidak ditemukan atau sudah kosong.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        docs.Delete(current);
        SetDokumenValue(siswa, field, null);
        await db.SaveChangesAsync();
        TempData["message"] = "Dokumen berhasil dihapus.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ------------------------------------------------------- Delete (arsip)

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var siswa = await db.Siswa.FindAsync(id);
        if (siswa is null) return NotFound($"Siswa dengan ID {id} tidak ditemukan.");

        // Arsipkan siswa TANPA menghapus identitas/riwayat - lihat 01-siswa-psb.md
        // §3.8: teks tombol/SweetAlert di UI bilang "Hapus"/"permanen" TAPI ini
        // cuma ubah status, SENGAJA dipertahankan apa adanya, jangan diperbaiki.
        siswa.Status = StatusSiswa.keluar;
        await db.SaveChangesAsync();
        TempData["message"] = $"Data siswa {siswa.Nama} dipindahkan ke arsip.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDelete(List<int>? ids)
    {
        var valid = (ids ?? []).Where(i => i > 0).Distinct().ToList();
        if (valid.Count == 0)
        {
            TempData["error"] = "ID siswa tidak valid.";
            return RedirectToAction(nameof(Index));
        }

        var count = await db.Siswa.Where(s => valid.Contains(s.SiswaId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Status, StatusSiswa.keluar));

        TempData["message"] = $"{count} data siswa dipindahkan ke arsip.";
        return RedirectToAction(nameof(Index));
    }

    // ---------------------------------------------------------------- Print

    [HttpGet("print")]
    public async Task<IActionResult> Print()
    {
        var rows = await db.Siswa.Include(s => s.Kelas)
            .Where(s => s.Status == StatusSiswa.aktif)
            .OrderBy(s => s.Nama)
            .Select(s => new SiswaPrintRow
            {
                Nis = s.Nis,
                Nama = s.Nama,
                JenisKelamin = s.JenisKelamin.ToString(),
                NamaKelas = s.Kelas != null ? s.Kelas.NamaKelas : null,
                TempatLahir = s.TempatLahir,
                TanggalLahir = s.TanggalLahir,
                AsalSekolah = s.AsalSekolah,
                Alamat = ((s.AlamatJalan ?? "") + " " +
                          (s.AlamatRt != null || s.AlamatRw != null ? $"RT {s.AlamatRt}/RW {s.AlamatRw} " : "") +
                          (s.AlamatKelurahan ?? "") + " " + (s.AlamatKecamatan ?? "")).Trim(),
                NamaAyah = s.NamaAyah,
                NamaIbu = s.NamaIbu,
                NoHandphone = s.NoHandphone,
            })
            .ToListAsync();
        return View(rows);
    }

    // --------------------------------------------------------------- Arsip

    [HttpGet("arsip")]
    public async Task<IActionResult> Arsip()
    {
        var siswaList = await db.Siswa.Include(s => s.Kelas)
            .Where(s => s.Status != StatusSiswa.aktif)
            .OrderBy(s => s.Nama)
            .ToListAsync();
        return View(siswaList.Select(ToRow).ToList());
    }

    // ------------------------------------------------------------- Import

    [HttpGet("import")]
    public async Task<IActionResult> Import()
    {
        HttpContext.Session.Remove(PreviewSessionKey);
        ViewBag.KelasOptions = await GetKelasOptionsAsync();
        return View();
    }

    [HttpGet("download-template")]
    public IActionResult DownloadTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Siswa");
        var headers = new[] { "Nama", "JK", "Nis", "NISN", "TTL", "ASAL TK", "Jalan", "RT", "RW", "Kel", "Kec", "Ayah", "Ibu", "Pekerjaan Ayah", "Pekerjaan Ibu", "No HP" };
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];

        // Kolom "berbentuk angka tapi sebenarnya ID/nomor telepon" (Nis, NISN,
        // No HP) DIPAKSA format Text ("@") SEBELUM diisi nilai apa pun - kalau
        // tidak, Excel/ClosedXML menyimpannya sbg angka murni, dan angka NOL DI
        // DEPAN hilang (mis. No HP "0812..." jadi "812...", NISN "0051..." jadi
        // "51...") - baik pas ngetik manual maupun copy-paste dari sumber lain
        // (dilaporkan user 2026-09-11: NIS/No HP dari luar selalu kehilangan
        // 0 di depan). Kolom C=Nis, D=NISN, P=No HP - RENTANG SAMPAI baris 1000
        // (bukan cuma 2-3 baris contoh) supaya baris BARU yang ditambah/ditempel
        // user sendiri ikut kena format ini, bukan cuma baris contoh bawaan.
        ws.Range("C2:C1000").Style.NumberFormat.Format = "@";
        ws.Range("D2:D1000").Style.NumberFormat.Format = "@";
        ws.Range("P2:P1000").Style.NumberFormat.Format = "@";

        var contoh1 = new object[] { "Budi Santoso", "L", "2024001", "0051234567", "Jakarta, 15 Mei 2018", "TK Al Ikhlas", "Jl. Merdeka No. 123", "001", "002", "Menteng", "Menteng", "Santoso", "Siti", "Wiraswasta", "Ibu Rumah Tangga", "081234567890" };
        var contoh2 = new object[] { "Siti Nurhaliza", "P", "2024002", "0051234568", "Bandung, 20 Agustus 2018", "TK Harapan Bunda", "Jl. Asia Afrika No. 456", "003", "004", "Braga", "Sumur Bandung", "Ahmad", "Dewi", "PNS", "Guru", "081298765432" };
        for (var i = 0; i < contoh1.Length; i++) { ws.Cell(2, i + 1).Value = contoh1[i].ToString(); ws.Cell(3, i + 1).Value = contoh2[i].ToString(); }

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "template_import_siswa.xlsx");
    }

    [HttpPost("process-import")]
    public async Task<IActionResult> PreviewImport(IFormFile? file, int? kelas_id)
    {
        if (file is null || file.Length == 0)
        {
            TempData["error"] = "File tidak ditemukan.";
            return RedirectToAction(nameof(Import));
        }
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
        {
            TempData["error"] = "Format file harus .xlsx atau .xls";
            return RedirectToAction(nameof(Import));
        }
        if (file.Length > 5 * 1024 * 1024)
        {
            TempData["error"] = "Ukuran file terlalu besar! Maksimal 5MB.";
            return RedirectToAction(nameof(Import));
        }

        Kelas? kelasTujuan = null;
        if (kelas_id is > 0)
        {
            kelasTujuan = await db.Kelas.FirstOrDefaultAsync(k => k.KelasId == kelas_id && k.IsActive);
            if (kelasTujuan is null)
            {
                TempData["error"] = "Kelas tujuan tidak valid atau sudah diarsipkan.";
                return RedirectToAction(nameof(Import));
            }
        }

        // PENTING: XLWorkbook (ClosedXML) itu IDisposable dan XLRow/XLCell bersifat LAZY
        // (baca dari internal workbook saat diakses, bukan saat RowsUsed() dipanggil) -
        // kalau workbook di-dispose duluan (keluar blok using) lalu Cell() diakses
        // belakangan, akan throw ObjectDisposedException. Semua sel WAJIB dibaca jadi
        // array string polos SELAGI workbook masih hidup, baru diproses di luar blok ini.
        List<(int RowNumber, string[] Cells)> rows;
        try
        {
            using var stream = file.OpenReadStream();
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();
            rows = ws.RowsUsed()
                .Select(row => (row.RowNumber(), Enumerable.Range(1, ExpectedHeaders.Length).Select(i => row.Cell(i).GetString().Trim()).ToArray()))
                .ToList();
        }
        catch (Exception ex)
        {
            TempData["error"] = $"Gagal membaca file Excel: {ex.Message}";
            return RedirectToAction(nameof(Import));
        }

        if (rows.Count == 0)
        {
            TempData["error"] = "Tidak ada baris valid untuk diimport.";
            return RedirectToAction(nameof(Import));
        }

        var actualHeaders = rows[0].Cells.Select(NormalizeHeader).ToArray();
        if (!actualHeaders.SequenceEqual(ExpectedHeaders))
        {
            TempData["error"] = "Urutan kolom tidak sesuai template terbaru. Download ulang template siswa sebelum mengimpor.";
            return RedirectToAction(nameof(Import));
        }

        var preview = new ImportPreviewSession { KelasId = kelasTujuan?.KelasId, NamaKelas = kelasTujuan?.NamaKelas };
        var siswaAll = await db.Siswa.ToListAsync(); // NIS matching TANPA filter status, lihat §3.13 poin 11

        for (var r = 1; r < rows.Count; r++)
        {
            var (rowNum, cells) = rows[r];
            string Cell(int i) => cells[i - 1];

            var nama = Cell(1);
            var jk = Cell(2).ToUpperInvariant();
            var nis = Cell(3);
            var nisn = Cell(4);
            var ttl = Cell(5);
            var asalSekolah = Cell(6);
            var jalan = Cell(7);
            var rt = Cell(8);
            var rw = Cell(9);
            var kel = Cell(10);
            var kec = Cell(11);
            var ayah = Cell(12);
            var ibu = Cell(13);
            var pekerjaanAyah = Cell(14);
            var pekerjaanIbu = Cell(15);
            var noHp = Regex.Replace(Cell(16), @"\D+", "");

            if (nama == "" && jk == "" && nis == "") continue; // baris kosong

            if (nis == "") { preview.Errors.Add($"Baris {rowNum}: NIS tidak boleh kosong"); preview.ErrorCount++; continue; }
            if (nama == "") { preview.Errors.Add($"Baris {rowNum}: Nama tidak boleh kosong"); preview.ErrorCount++; continue; }
            if (jk != "L" && jk != "P") { preview.Errors.Add($"Baris {rowNum}: Jenis kelamin harus L atau P"); preview.ErrorCount++; continue; }

            string? tempatLahir = null;
            DateOnly? tanggalLahir = null;
            if (ttl != "")
            {
                (tempatLahir, tanggalLahir) = ParseTtl(ttl);
            }

            var existing = siswaAll.FirstOrDefault(s => s.Nis == nis);
            var data = new Dictionary<string, string?>();

            if (existing is not null)
            {
                var diff = new List<ImportDiffField>();
                void Track(string field, string? lama, string? baru)
                {
                    data[field] = baru;
                    if (lama != baru)
                    {
                        diff.Add(new ImportDiffField { Field = field, Label = ImportFieldLabels.Map[field], Lama = lama, Baru = baru });
                    }
                }

                // nama & jenis_kelamin SELALU ditulis ulang, field lain: kosong = jangan diubah.
                Track("nama", existing.Nama, nama);
                Track("jenis_kelamin", existing.JenisKelamin.ToString(), jk);
                if (nisn != "") Track("nisn", existing.Nisn, nisn);
                if (ttl != "") { Track("tempat_lahir", existing.TempatLahir, tempatLahir); data["tanggal_lahir"] = tanggalLahir?.ToString("yyyy-MM-dd"); }
                if (asalSekolah != "") Track("asal_sekolah", existing.AsalSekolah, asalSekolah);
                if (jalan != "") Track("alamat_jalan", existing.AlamatJalan, jalan);
                if (rt != "") Track("alamat_rt", existing.AlamatRt, rt);
                if (rw != "") Track("alamat_rw", existing.AlamatRw, rw);
                if (kel != "") Track("alamat_kelurahan", existing.AlamatKelurahan, kel);
                if (kec != "") Track("alamat_kecamatan", existing.AlamatKecamatan, kec);
                if (ayah != "") Track("nama_ayah", existing.NamaAyah, ayah);
                if (pekerjaanAyah != "") Track("pekerjaan_ayah", existing.PekerjaanAyah, pekerjaanAyah);
                if (ibu != "") Track("nama_ibu", existing.NamaIbu, ibu);
                if (pekerjaanIbu != "") Track("pekerjaan_ibu", existing.PekerjaanIbu, pekerjaanIbu);
                if (noHp != "") Track("no_handphone", existing.NoHandphone, noHp);

                preview.Rows.Add(new ImportPreviewRow { RowNumber = rowNum, Nis = nis, Nama = nama, Type = "update", ExistingSiswaId = existing.SiswaId, Diff = diff, Data = data });
                preview.UpdateCount++;
            }
            else
            {
                if (noHp == "")
                {
                    preview.Errors.Add($"Baris {rowNum}: No HP wajib diisi (dipakai akun Orang Tua di aplikasi HP)");
                    preview.ErrorCount++;
                    continue;
                }
                data["nama"] = nama;
                data["jenis_kelamin"] = jk;
                data["nis"] = nis;
                data["nisn"] = nisn == "" ? null : nisn;
                data["tempat_lahir"] = tempatLahir;
                data["tanggal_lahir"] = tanggalLahir?.ToString("yyyy-MM-dd");
                data["asal_sekolah"] = asalSekolah == "" ? null : asalSekolah;
                data["alamat_jalan"] = jalan == "" ? null : jalan;
                data["alamat_rt"] = rt == "" ? null : rt;
                data["alamat_rw"] = rw == "" ? null : rw;
                data["alamat_kelurahan"] = kel == "" ? null : kel;
                data["alamat_kecamatan"] = kec == "" ? null : kec;
                data["nama_ayah"] = ayah == "" ? null : ayah;
                data["pekerjaan_ayah"] = pekerjaanAyah == "" ? null : pekerjaanAyah;
                data["nama_ibu"] = ibu == "" ? null : ibu;
                data["pekerjaan_ibu"] = pekerjaanIbu == "" ? null : pekerjaanIbu;
                data["no_handphone"] = noHp;

                preview.Rows.Add(new ImportPreviewRow { RowNumber = rowNum, Nis = nis, Nama = nama, Type = "insert", Data = data });
                preview.InsertCount++;
            }
        }

        if (preview.Rows.Count == 0)
        {
            if (preview.Errors.Count > 0) TempData["import_errors"] = JsonSerializer.Serialize(preview.Errors);
            TempData["error"] = "Tidak ada baris valid untuk diimport.";
            return RedirectToAction(nameof(Import));
        }

        HttpContext.Session.SetString(PreviewSessionKey, JsonSerializer.Serialize(preview));
        return RedirectToAction(nameof(ShowPreviewImport));
    }

    [HttpGet("preview-import")]
    public IActionResult ShowPreviewImport()
    {
        var json = HttpContext.Session.GetString(PreviewSessionKey);
        if (json is null)
        {
            TempData["error"] = "Belum ada data untuk di-preview. Silakan upload ulang.";
            return RedirectToAction(nameof(Import));
        }
        var preview = JsonSerializer.Deserialize<ImportPreviewSession>(json)!;
        return View("PreviewImport", preview);
    }

    [HttpPost("apply-import")]
    public async Task<IActionResult> ApplyImport()
    {
        var json = HttpContext.Session.GetString(PreviewSessionKey);
        if (json is null)
        {
            TempData["error"] = "Belum ada data untuk diterapkan. Silakan upload ulang.";
            return RedirectToAction(nameof(Import));
        }
        var preview = JsonSerializer.Deserialize<ImportPreviewSession>(json)!;

        await using var tx = await db.Database.BeginTransactionAsync();
        int inserted = 0, updated = 0;
        try
        {
            foreach (var row in preview.Rows)
            {
                if (row.Type == "insert")
                {
                    db.Siswa.Add(new Siswa
                    {
                        Nama = row.Data["nama"]!,
                        JenisKelamin = Enum.Parse<JenisKelamin>(row.Data["jenis_kelamin"]!),
                        Nis = row.Data["nis"]!,
                        Nisn = row.Data.GetValueOrDefault("nisn"),
                        KelasId = preview.KelasId,
                        Status = StatusSiswa.aktif,
                        TempatLahir = row.Data.GetValueOrDefault("tempat_lahir"),
                        TanggalLahir = ParseDateOrNull(row.Data.GetValueOrDefault("tanggal_lahir")),
                        AsalSekolah = row.Data.GetValueOrDefault("asal_sekolah"),
                        AlamatJalan = row.Data.GetValueOrDefault("alamat_jalan"),
                        AlamatRt = row.Data.GetValueOrDefault("alamat_rt"),
                        AlamatRw = row.Data.GetValueOrDefault("alamat_rw"),
                        AlamatKelurahan = row.Data.GetValueOrDefault("alamat_kelurahan"),
                        AlamatKecamatan = row.Data.GetValueOrDefault("alamat_kecamatan"),
                        NamaAyah = row.Data.GetValueOrDefault("nama_ayah"),
                        PekerjaanAyah = row.Data.GetValueOrDefault("pekerjaan_ayah"),
                        NamaIbu = row.Data.GetValueOrDefault("nama_ibu"),
                        PekerjaanIbu = row.Data.GetValueOrDefault("pekerjaan_ibu"),
                        NoHandphone = row.Data.GetValueOrDefault("no_handphone"),
                    });
                    inserted++;
                }
                else
                {
                    var siswa = await db.Siswa.FindAsync(row.ExistingSiswaId);
                    if (siswa is null) continue;
                    ApplyIfPresent(row.Data, "nama", v => siswa.Nama = v!);
                    ApplyIfPresent(row.Data, "jenis_kelamin", v => siswa.JenisKelamin = Enum.Parse<JenisKelamin>(v!));
                    if (row.Data.ContainsKey("nisn")) siswa.Nisn = row.Data["nisn"];
                    if (row.Data.ContainsKey("tempat_lahir")) siswa.TempatLahir = row.Data["tempat_lahir"];
                    if (row.Data.ContainsKey("tanggal_lahir")) siswa.TanggalLahir = ParseDateOrNull(row.Data["tanggal_lahir"]);
                    if (row.Data.ContainsKey("asal_sekolah")) siswa.AsalSekolah = row.Data["asal_sekolah"];
                    if (row.Data.ContainsKey("alamat_jalan")) siswa.AlamatJalan = row.Data["alamat_jalan"];
                    if (row.Data.ContainsKey("alamat_rt")) siswa.AlamatRt = row.Data["alamat_rt"];
                    if (row.Data.ContainsKey("alamat_rw")) siswa.AlamatRw = row.Data["alamat_rw"];
                    if (row.Data.ContainsKey("alamat_kelurahan")) siswa.AlamatKelurahan = row.Data["alamat_kelurahan"];
                    if (row.Data.ContainsKey("alamat_kecamatan")) siswa.AlamatKecamatan = row.Data["alamat_kecamatan"];
                    if (row.Data.ContainsKey("nama_ayah")) siswa.NamaAyah = row.Data["nama_ayah"];
                    if (row.Data.ContainsKey("pekerjaan_ayah")) siswa.PekerjaanAyah = row.Data["pekerjaan_ayah"];
                    if (row.Data.ContainsKey("nama_ibu")) siswa.NamaIbu = row.Data["nama_ibu"];
                    if (row.Data.ContainsKey("pekerjaan_ibu")) siswa.PekerjaanIbu = row.Data["pekerjaan_ibu"];
                    if (row.Data.ContainsKey("no_handphone")) siswa.NoHandphone = row.Data["no_handphone"];
                    // KelasId & Status SENGAJA tidak disentuh - lihat §3.13 poin 11.
                    updated++;
                }
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            TempData["error"] = "Import gagal diterapkan, tidak ada data yang berubah.";
            return RedirectToAction(nameof(Index));
        }

        HttpContext.Session.Remove(PreviewSessionKey);

        var message = $"Berhasil import {inserted} siswa baru.";
        if (updated > 0) message += $" {updated} siswa lama diperbarui.";
        TempData["message"] = message;
        if (preview.Errors.Count > 0) TempData["import_errors"] = JsonSerializer.Serialize(preview.Errors);
        return RedirectToAction(nameof(Index));
    }

    private static void ApplyIfPresent(Dictionary<string, string?> data, string key, Action<string?> apply)
    {
        if (data.TryGetValue(key, out var v)) apply(v);
    }

    private static DateOnly? ParseDateOrNull(string? s) =>
        string.IsNullOrEmpty(s) ? null : DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------- Helper

    // SEBELUMNYA: kalau tidak ada koma ATAU bagian tanggal gagal di-parse,
    // fungsi ini balikin (null, null), dan PEMANGGIL menolak SELURUH BARIS
    // ("TTL harus berformat...") - padahal petunjuk sendiri bilang TTL itu
    // OPSIONAL. Bug nyata ditemukan 2026-09-11: import 17 siswa TKIT GAGAL
    // TOTAL ("Tidak ada baris valid") krn kolom TTL cuma diisi nama kota
    // ("Jakarta") tanpa tanggal - data lain (nama/NIS/JK/dst) semuanya valid,
    // tapi ikut terbuang gara2 1 kolom opsional ini. Sekarang best-effort:
    // TIDAK PERNAH menolak baris krn TTL - tempat SELALU terisi (apa adanya
    // kalau tidak ada koma/tanggal tidak valid), tanggal cuma diisi kalau
    // benar2 berhasil di-parse, null kalau tidak (bukan alasan buang baris).
    private static (string? tempat, DateOnly? tanggal) ParseTtl(string ttl)
    {
        var parts = ttl.Split(',');
        if (parts.Length < 2) return (ttl.Trim(), null);
        var tanggalRaw = parts[^1].Trim();
        var tempat = string.Join(",", parts[..^1]).Trim();
        var tanggal = IndonesianDateService.ParseIndonesianDate(tanggalRaw);
        return tanggal is null ? (ttl.Trim(), null) : (tempat, tanggal);
    }

    private static string NormalizeHeader(string h) => Regex.Replace(h.Trim(), @"\s+", " ").ToUpperInvariant();

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? OnlyDigits(string? s) => string.IsNullOrEmpty(s) ? s : Regex.Replace(s, @"\D+", "");

    private async Task<List<KelasOption>> GetKelasOptionsAsync() =>
        await db.Kelas.Where(k => k.IsActive)
            .OrderBy(k => k.Tingkat).ThenBy(k => k.NamaKelas)
            .Select(k => new KelasOption { KelasId = k.KelasId, NamaKelas = k.NamaKelas })
            .ToListAsync();

    private static bool IsNumericLike(string s) => Regex.IsMatch(s, @"^-?\d+(\.\d+)?$");

    private async Task<Dictionary<string, string>> ValidateFormAsync(SiswaFormInput input, int? excludeId)
    {
        var e = new Dictionary<string, string>();

        var nama = (input.Nama ?? "").Trim();
        if (nama == "") e["Nama"] = "Nama wajib diisi.";
        else if (nama.Length < 3) e["Nama"] = "Nama minimal 3 karakter.";
        else if (nama.Length > 100) e["Nama"] = "Nama maksimal 100 karakter.";
        else if (!Regex.IsMatch(nama, ValidationPatterns.RegexNama)) e["Nama"] = "Nama " + ValidationPatterns.PesanRegexNama;

        if (input.JenisKelamin != "L" && input.JenisKelamin != "P") e["JenisKelamin"] = "Jenis kelamin wajib dipilih.";

        var nis = (input.Nis ?? "").Trim();
        if (nis == "") e["Nis"] = "NIS wajib diisi.";
        else if (nis.Length > 20) e["Nis"] = "NIS maksimal 20 karakter.";
        else if (!IsNumericLike(nis)) e["Nis"] = "NIS hanya boleh berisi angka.";
        else if (await db.Siswa.AnyAsync(s => s.Nis == nis && s.SiswaId != (excludeId ?? 0))) e["Nis"] = "NIS ini sudah dipakai siswa lain.";

        if (!string.IsNullOrWhiteSpace(input.Nisn))
        {
            if (input.Nisn.Length > 20) e["Nisn"] = "NISN maksimal 20 karakter.";
            else if (!IsNumericLike(input.Nisn)) e["Nisn"] = "NISN hanya boleh berisi angka.";
        }

        if (!string.IsNullOrWhiteSpace(input.TempatLahir))
        {
            if (input.TempatLahir.Length > 100) e["TempatLahir"] = "Tempat lahir maksimal 100 karakter.";
            else if (!Regex.IsMatch(input.TempatLahir, ValidationPatterns.RegexTeksPendek)) e["TempatLahir"] = "Tempat lahir " + ValidationPatterns.PesanRegexTeksPendek;
        }

        if (!string.IsNullOrWhiteSpace(input.AsalSekolah) && input.AsalSekolah.Length > 150) e["AsalSekolah"] = "Asal sekolah maksimal 150 karakter.";
        if (!string.IsNullOrWhiteSpace(input.AlamatJalan) && input.AlamatJalan.Length > 255) e["AlamatJalan"] = "Alamat maksimal 255 karakter.";

        if (!string.IsNullOrWhiteSpace(input.AlamatRt))
        {
            if (input.AlamatRt.Length > 5) e["AlamatRt"] = "RT maksimal 5 karakter.";
            else if (!IsNumericLike(input.AlamatRt)) e["AlamatRt"] = "RT hanya boleh berisi angka.";
        }
        if (!string.IsNullOrWhiteSpace(input.AlamatRw))
        {
            if (input.AlamatRw.Length > 5) e["AlamatRw"] = "RW maksimal 5 karakter.";
            else if (!IsNumericLike(input.AlamatRw)) e["AlamatRw"] = "RW hanya boleh berisi angka.";
        }
        if (!string.IsNullOrWhiteSpace(input.AlamatKelurahan))
        {
            if (input.AlamatKelurahan.Length > 100) e["AlamatKelurahan"] = "Kelurahan maksimal 100 karakter.";
            else if (!Regex.IsMatch(input.AlamatKelurahan, ValidationPatterns.RegexTeksPendek)) e["AlamatKelurahan"] = "Kelurahan " + ValidationPatterns.PesanRegexTeksPendek;
        }
        if (!string.IsNullOrWhiteSpace(input.AlamatKecamatan))
        {
            if (input.AlamatKecamatan.Length > 100) e["AlamatKecamatan"] = "Kecamatan maksimal 100 karakter.";
            else if (!Regex.IsMatch(input.AlamatKecamatan, ValidationPatterns.RegexTeksPendek)) e["AlamatKecamatan"] = "Kecamatan " + ValidationPatterns.PesanRegexTeksPendek;
        }

        if (!string.IsNullOrWhiteSpace(input.NamaAyah))
        {
            if (input.NamaAyah.Length > 100) e["NamaAyah"] = "Nama ayah maksimal 100 karakter.";
            else if (!Regex.IsMatch(input.NamaAyah, ValidationPatterns.RegexNama)) e["NamaAyah"] = "Nama ayah " + ValidationPatterns.PesanRegexNama;
        }
        if (!string.IsNullOrWhiteSpace(input.PekerjaanAyah))
        {
            if (input.PekerjaanAyah.Length > 100) e["PekerjaanAyah"] = "Pekerjaan ayah maksimal 100 karakter.";
            else if (!Regex.IsMatch(input.PekerjaanAyah, ValidationPatterns.RegexTeksPendek)) e["PekerjaanAyah"] = "Pekerjaan ayah " + ValidationPatterns.PesanRegexTeksPendek;
        }
        if (!string.IsNullOrWhiteSpace(input.NamaIbu))
        {
            if (input.NamaIbu.Length > 100) e["NamaIbu"] = "Nama ibu maksimal 100 karakter.";
            else if (!Regex.IsMatch(input.NamaIbu, ValidationPatterns.RegexNama)) e["NamaIbu"] = "Nama ibu " + ValidationPatterns.PesanRegexNama;
        }
        if (!string.IsNullOrWhiteSpace(input.PekerjaanIbu))
        {
            if (input.PekerjaanIbu.Length > 100) e["PekerjaanIbu"] = "Pekerjaan ibu maksimal 100 karakter.";
            else if (!Regex.IsMatch(input.PekerjaanIbu, ValidationPatterns.RegexTeksPendek)) e["PekerjaanIbu"] = "Pekerjaan ibu " + ValidationPatterns.PesanRegexTeksPendek;
        }

        // No Handphone WAJIB sejak 2026-09-07 - lihat 01-siswa-psb.md §3.4 & §6 poin 2.
        var noHp = (input.NoHandphone ?? "").Trim();
        if (noHp == "") e["NoHandphone"] = "No Handphone wajib diisi - dipakai utk akun Orang Tua di aplikasi HP.";
        else if (noHp.Length > 20) e["NoHandphone"] = "No Handphone maksimal 20 karakter.";
        else if (!IsNumericLike(Regex.Replace(noHp, @"\D+", ""))) e["NoHandphone"] = "No Handphone hanya boleh berisi angka.";

        foreach (var (file, label) in new (IFormFile?, string)[]
                 {
                     (input.DokumenKk, "KK"), (input.DokumenAkta, "Akta"),
                     (input.DokumenKia, "KIA"), (input.DokumenIjazah, "Ijazah"),
                 })
        {
            if (file is null || file.Length == 0) continue;
            if (file.Length > 2 * 1024 * 1024) { e[$"Dokumen{label}"] = $"Ukuran dokumen {label} maksimal 2MB."; continue; }
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext is not (".pdf" or ".jpg" or ".jpeg" or ".png")) { e[$"Dokumen{label}"] = $"Dokumen {label} harus berupa file PDF, JPG, atau PNG."; continue; }
            if (!docs.IsValidDocument(file)) e[$"Dokumen{label}"] = $"Tipe/MIME dokumen {label} tidak valid.";
        }

        return e;
    }
}
