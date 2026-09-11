using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DataMaster.Data;
using DataMaster.Data.Entities;
using DataMaster.Web.Models.Guru;
using DataMaster.Web.Models.Siswa;
using DataMaster.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Controllers;

// Port 1:1 dari app/Controllers/Guru.php - lihat 02-guru-kelas-struktur.md §3-4.
// LINGKUP: install_type "perusahaan" BELUM didukung di porting ini (tidak ada
// pengaturan install_type sama sekali) - controller ini SELALU berperilaku seperti
// instalasi "pendidikan" (semua jabatan diizinkan, label "Data Guru & Pegawai").
// Wali Kelas dibaca dari tabel WaliKelas (skema sudah ada) walau UI penetapannya
// belum dibangun - jadi kolom ini akan selalu kosong sampai modul PenugasanMengajar
// dibuat, TAPI logic baca & guard di sini sudah benar sejak sekarang (forward-compatible).
[Authorize(Roles = "admin")]
[Route("guru")]
public class GuruController(DataMasterDbContext db, WaliKelasService waliKelas) : Controller
{
    private static readonly int[] AllowedPerPage = [50, 100, 150, 200];
    private static readonly string[] StatusKeluarValid = ["purna_bakti", "resign", "diberhentikan"];
    private static readonly string[] ExpectedHeaders = ["NAMA", "JENIS KELAMIN", "JABATAN", "GELAR TERAKHIR", "NO HP"];

    private const string PreviewSessionKey = "preview_import_guru";

    private async Task<int?> GetTahunAktifIdAsync() =>
        (await db.TahunAjaran.FirstOrDefaultAsync(t => t.IsActive))?.TahunAjaranId;

    private async Task<string?> GetNamaKelasWaliAsync(int guruId)
    {
        var taId = await GetTahunAktifIdAsync();
        if (taId is null) return null;
        return (await waliKelas.GetKelasByGuruAsync(guruId, taId))?.NamaKelas;
    }

    // ---------------------------------------------------------------- Index

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, int? per_page, int? page, string? ajax)
    {
        var keyword = (q ?? "").Trim();
        var perPage = per_page.HasValue && AllowedPerPage.Contains(per_page.Value) ? per_page.Value : 50;
        var pageNum = Math.Max(1, page ?? 1);

        var model = new GuruIndexViewModel { Keyword = keyword, PerPage = perPage };
        var taId = await GetTahunAktifIdAsync();

        async Task<List<GuruRow>> ToRowsFromDbAsync(IQueryable<Data.Entities.Guru> q2) => await ToRowsAsync(await q2.ToListAsync());

        async Task<List<GuruRow>> ToRowsAsync(List<Data.Entities.Guru> guruList)
        {
            // Bug nyata KEMBAR dgn Print() (lihat catatan di sana) - .Include(w =>
            // w.Kelas) WAJIB sebelum ToDictionaryAsync, kalau tidak NRE begitu ADA
            // baris WaliKelas yang cocok filter (tidak pernah ter-trigger sebelum
            // ada wali kelas ter-assign, makanya luput dari uji coba awal).
            var waliMap = taId is null
                ? new Dictionary<int, string>()
                : await db.WaliKelas.Include(w => w.Kelas).Where(w => w.TahunAjaranId == taId && guruList.Select(g => g.GuruId).Contains(w.GuruId))
                    .ToDictionaryAsync(w => w.GuruId, w => w.Kelas.NamaKelas);
            return guruList.Select(g => new GuruRow
            {
                GuruId = g.GuruId,
                Nama = g.Nama,
                Nip = g.Nip,
                JenisKelamin = g.JenisKelamin?.ToString(),
                Jabatan = g.Jabatan.ToString(),
                NoHandphone = g.NoHandphone,
                NamaKelasWali = waliMap.GetValueOrDefault(g.GuruId),
            }).ToList();
        }

        if (keyword != "")
        {
            var semua = await db.Guru.Where(g => g.StatusAktif).OrderBy(g => g.Nama).ToListAsync();
            var hasil = semua.Where(g => TextSearchService.MatchesAny(keyword, g.Nama, g.Nip, g.NoHandphone)).ToList();
            model.Rows = await ToRowsAsync(hasil);
            model.Total = hasil.Count;
            model.TotalGuruKelas = hasil.Count(g => g.Jabatan == JabatanGuru.guru_kelas);
            model.TotalGuruBidang = hasil.Count(g => g.Jabatan == JabatanGuru.guru_bidang);
            model.TotalKaryawan = hasil.Count(g => g.Jabatan == JabatanGuru.karyawan);
            model.Page = 1;
            model.TotalPages = 1;
        }
        else
        {
            var aktifQuery = db.Guru.Where(g => g.StatusAktif);
            model.Total = await aktifQuery.CountAsync();
            model.TotalGuruKelas = await aktifQuery.CountAsync(g => g.Jabatan == JabatanGuru.guru_kelas);
            model.TotalGuruBidang = await aktifQuery.CountAsync(g => g.Jabatan == JabatanGuru.guru_bidang);
            model.TotalKaryawan = await aktifQuery.CountAsync(g => g.Jabatan == JabatanGuru.karyawan);
            model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.Total / (double)perPage));
            model.Page = Math.Min(pageNum, model.TotalPages);
            var offset = (model.Page - 1) * perPage;
            model.Rows = await ToRowsFromDbAsync(db.Guru.Where(g => g.StatusAktif).OrderBy(g => g.Nama).Skip(offset).Take(perPage));
        }

        if (TempData["import_errors"] is string errJson)
        {
            model.ImportErrors = JsonSerializer.Deserialize<List<string>>(errJson) ?? [];
        }

        if (ajax == "1") return PartialView("_Hasil", model);
        return View(model);
    }

    // --------------------------------------------------------------- Create

    [HttpGet("create")]
    public IActionResult Create() => View(new GuruFormViewModel());

    [HttpPost("store")]
    public async Task<IActionResult> Store(GuruFormInput input)
    {
        var errors = await ValidateFormAsync(input, excludeId: null);
        if (errors.Count > 0)
        {
            return View("Create", new GuruFormViewModel { Input = input, Errors = errors });
        }

        var jabatan = input.Jabatan == "karyawan" ? JabatanGuru.karyawan : JabatanGuru.guru_bidang;
        var guru = new Data.Entities.Guru
        {
            Nama = input.Nama.Trim(),
            Nip = NullIfEmpty(input.Nip),
            JenisKelamin = string.IsNullOrEmpty(input.JenisKelamin) ? null : Enum.Parse<JenisKelamin>(input.JenisKelamin),
            Jabatan = jabatan,
            MataPelajaranCatatan = NullIfEmpty(input.MataPelajaran),
            GelarTerakhir = NullIfEmpty(input.GelarTerakhir),
            NoHandphone = OnlyDigits(input.NoHandphone),
            Alamat = NullIfEmpty(input.Alamat),
            StatusAktif = true,
        };
        db.Guru.Add(guru);
        await db.SaveChangesAsync();

        TempData["message"] = "Data guru berhasil ditambahkan.";
        return RedirectToAction(nameof(Index));
    }

    // --------------------------------------------------------------- Detail

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var guru = await db.Guru.FindAsync(id);
        if (guru is null) return NotFound($"Guru dengan ID {id} tidak ditemukan.");

        var vm = await ToFormViewModelAsync(guru);
        return View(vm);
    }

    private async Task<GuruFormViewModel> ToFormViewModelAsync(Data.Entities.Guru guru) => new()
    {
        Input = new GuruFormInput
        {
            GuruId = guru.GuruId,
            Nama = guru.Nama,
            Nip = guru.Nip,
            JenisKelamin = guru.JenisKelamin?.ToString(),
            Jabatan = guru.Jabatan == JabatanGuru.karyawan ? "karyawan" : "guru",
            MataPelajaran = guru.MataPelajaranCatatan,
            GelarTerakhir = guru.GelarTerakhir,
            NoHandphone = guru.NoHandphone,
            Alamat = guru.Alamat,
            StatusAktif = guru.StatusAktif,
        },
        NamaKelasWali = await GetNamaKelasWaliAsync(guru.GuruId),
        JabatanDbSaatIni = guru.Jabatan.ToString(),
        CreatedAt = guru.CreatedAt,
    };

    [HttpPost("{id:int}/update")]
    public async Task<IActionResult> Update(int id, GuruFormInput input)
    {
        var guru = await db.Guru.FindAsync(id);
        if (guru is null) return NotFound($"Guru dengan ID {id} tidak ditemukan.");

        var errors = await ValidateFormAsync(input, excludeId: id);
        if (errors.Count > 0)
        {
            var vm = await ToFormViewModelAsync(guru);
            vm.Input = input;
            vm.Errors = errors;
            return View("Detail", vm);
        }

        // jabatanForm==='karyawan' -> karyawan. Else ('guru' dipilih) -> PERTAHANKAN
        // guru_kelas/guru_bidang yang SUDAH ADA (nilai lama TIDAK PERNAH diketik ulang
        // manual, derivasi otomatis dari Kelola Kelas) - lihat §3.7.
        var jabatanBaru = input.Jabatan == "karyawan"
            ? JabatanGuru.karyawan
            : (guru.Jabatan == JabatanGuru.karyawan ? JabatanGuru.guru_bidang : guru.Jabatan);

        // Guard: guru masih tercatat wali kelas TAPI jabatan baru bukan guru_kelas/guru_bidang -> TOLAK.
        var namaKelasWali = await GetNamaKelasWaliAsync(id);
        if (namaKelasWali is not null && jabatanBaru is not (JabatanGuru.guru_kelas or JabatanGuru.guru_bidang))
        {
            TempData["error"] = $"{guru.Nama} masih tercatat sbg wali kelas {namaKelasWali}. Tetapkan wali kelas pengganti dulu di menu Kelola Kelas sebelum mengubah jabatannya.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        guru.Nama = input.Nama.Trim();
        guru.Nip = NullIfEmpty(input.Nip);
        guru.JenisKelamin = string.IsNullOrEmpty(input.JenisKelamin) ? null : Enum.Parse<JenisKelamin>(input.JenisKelamin);
        guru.Jabatan = jabatanBaru;
        guru.MataPelajaranCatatan = NullIfEmpty(input.MataPelajaran);
        guru.GelarTerakhir = NullIfEmpty(input.GelarTerakhir);
        guru.NoHandphone = OnlyDigits(input.NoHandphone);
        guru.Alamat = NullIfEmpty(input.Alamat);
        // Checkbox HTML: tidak dicentang = tidak terkirim sama sekali.
        guru.StatusAktif = input.StatusAktif;

        await db.SaveChangesAsync();
        TempData["message"] = "Data guru berhasil diperbarui.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------- Delete (arsip)

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, string? status_keluar)
    {
        var guru = await db.Guru.FindAsync(id);
        if (guru is null) return NotFound($"Guru dengan ID {id} tidak ditemukan.");

        guru.StatusAktif = false;
        if (StatusKeluarValid.Contains(status_keluar))
        {
            guru.StatusKeluar = Enum.Parse<StatusKeluarGuru>(status_keluar!);
        }
        await db.SaveChangesAsync();

        TempData["message"] = $"Data guru {guru.Nama} berhasil dinonaktifkan.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDelete(List<int>? ids, string? status_keluar)
    {
        var valid = (ids ?? []).Where(i => i > 0).Distinct().ToList();
        if (valid.Count == 0)
        {
            TempData["error"] = "Tidak ada data yang dipilih.";
            return RedirectToAction(nameof(Index));
        }

        var guruList = await db.Guru.Where(g => valid.Contains(g.GuruId)).ToListAsync();
        var keluar = StatusKeluarValid.Contains(status_keluar) ? Enum.Parse<StatusKeluarGuru>(status_keluar!) : (StatusKeluarGuru?)null;
        foreach (var g in guruList)
        {
            g.StatusAktif = false;
            if (keluar is not null) g.StatusKeluar = keluar;
        }
        await db.SaveChangesAsync();

        TempData["message"] = $"{guruList.Count} data guru berhasil dinonaktifkan.";
        return RedirectToAction(nameof(Index));
    }

    // --------------------------------------------------------------- Arsip

    [HttpGet("arsip")]
    public async Task<IActionResult> Arsip()
    {
        var guruList = await db.Guru.Where(g => !g.StatusAktif).OrderBy(g => g.Nama).ToListAsync();
        var rows = guruList.Select(g => new GuruRow
        {
            GuruId = g.GuruId, Nama = g.Nama, Nip = g.Nip, JenisKelamin = g.JenisKelamin?.ToString(),
            Jabatan = g.Jabatan.ToString(), NoHandphone = g.NoHandphone,
        }).ToList();
        return View(rows);
    }

    // ---------------------------------------------------------------- Print

    [HttpGet("print")]
    public async Task<IActionResult> Print()
    {
        var taId = await GetTahunAktifIdAsync();
        var guruList = await db.Guru.Where(g => g.StatusAktif).OrderBy(g => g.Nama).ToListAsync();
        // Bug nyata ditemukan (2026-09-08, testing menyeluruh): ToDictionaryAsync
        // BUKAN operator yg bisa diterjemahkan EF Core ke SQL - baris WaliKelas
        // ditarik dulu TANPA include, baru value-selector "w.Kelas.NamaKelas"
        // dievaluasi di SISI CLIENT dimana w.Kelas MASIH null (navigasi tidak
        // pernah di-Include) -> NullReferenceException nyata setiap kali ADA wali
        // kelas ter-assign. Fix: .Include(w => w.Kelas) SEBELUM ditarik ke memori.
        var waliMap = taId is null
            ? new Dictionary<int, string>()
            : await db.WaliKelas.Include(w => w.Kelas).Where(w => w.TahunAjaranId == taId).ToDictionaryAsync(w => w.GuruId, w => w.Kelas.NamaKelas);

        var rows = guruList.Select(g => new GuruPrintRow
        {
            Nama = g.Nama, Nip = g.Nip, JenisKelamin = g.JenisKelamin?.ToString(), Jabatan = g.Jabatan.ToString(),
            NamaKelasWali = waliMap.GetValueOrDefault(g.GuruId), MataPelajaran = g.MataPelajaranCatatan,
            NoHandphone = g.NoHandphone, Alamat = g.Alamat,
        }).ToList();
        return View(rows);
    }

    // ------------------------------------------------------------- Import

    [HttpGet("import")]
    public IActionResult Import()
    {
        HttpContext.Session.Remove(PreviewSessionKey);
        return View();
    }

    [HttpGet("download-template")]
    public IActionResult DownloadTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Guru");
        var headers = new[] { "Nama", "Jenis Kelamin", "Jabatan", "Gelar Terakhir", "No HP" };
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        // Kolom No HP (E) DIPAKSA format Text ("@") - lihat catatan lengkap di
        // SiswaController.DownloadTemplate(), masalah & alasan yang sama persis
        // (angka 0 di depan hilang kalau kolom dibiarkan format Angka biasa).
        ws.Range("E2:E1000").Style.NumberFormat.Format = "@";
        var c1 = new[] { "Rudi", "L", "Guru", "S1", "081234560001" };
        var c2 = new[] { "Siti Aisyah", "P", "Karyawan", "", "081234560002" };
        for (var i = 0; i < c1.Length; i++) { ws.Cell(2, i + 1).Value = c1[i]; ws.Cell(3, i + 1).Value = c2[i]; }

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "template_import_guru.xlsx");
    }

    [HttpPost("process-import")]
    public async Task<IActionResult> PreviewImport(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["error"] = "File tidak ditemukan.";
            return RedirectToAction(nameof(Import));
        }
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
        {
            TempData["error"] = "Format file harus .xlsx atau .xls.";
            return RedirectToAction(nameof(Import));
        }
        if (file.Length > 5 * 1024 * 1024)
        {
            TempData["error"] = "Ukuran file terlalu besar. Maksimal 5 MB.";
            return RedirectToAction(nameof(Import));
        }

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
            TempData["error"] = "Urutan kolom tidak sesuai template terbaru. Download ulang template guru sebelum mengimpor.";
            return RedirectToAction(nameof(Import));
        }

        var preview = new GuruImportPreviewSession();
        var guruAktif = await db.Guru.Where(g => g.StatusAktif).ToListAsync();
        var seenPhones = new HashSet<string>();

        for (var r = 1; r < rows.Count; r++)
        {
            var (rowNum, cells) = rows[r];
            string Cell(int i) => cells[i - 1];

            var nama = Cell(1);
            var jkRaw = Cell(2);
            var jabatanRaw = Cell(3);
            var gelar = Cell(4);
            var phone = Regex.Replace(Cell(5), @"\D+", "");

            if (nama == "" && phone == "") continue;
            if (nama == "") { preview.Errors.Add($"Baris {rowNum}: Nama wajib diisi."); preview.ErrorCount++; continue; }
            if (nama.Length > 100 || gelar.Length > 100 || phone.Length > 20)
            {
                preview.Errors.Add($"Baris {rowNum}: Nama/gelar maksimal 100 karakter dan No HP maksimal 20 angka.");
                preview.ErrorCount++; continue;
            }

            string? jk = NormalizeJenisKelaminImport(jkRaw);
            if (jk is null && jkRaw != "")
            {
                preview.Errors.Add($"Baris {rowNum}: Jenis Kelamin cuma boleh 'L'/'Laki-laki' atau 'P'/'Perempuan' (boleh dikosongkan).");
                preview.ErrorCount++; continue;
            }

            var jabatan = NormalizeJabatanImport(jabatanRaw);
            if (jabatan is null)
            {
                preview.Errors.Add($"Baris {rowNum}: Jabatan cuma boleh 'Guru' atau 'Karyawan' (boleh dikosongkan, default Guru).");
                preview.ErrorCount++; continue;
            }

            if (phone == "") { preview.Errors.Add($"Baris {rowNum}: No HP wajib diisi."); preview.ErrorCount++; continue; }

            var existing = guruAktif.FirstOrDefault(g => g.NoHandphone == phone);
            var data = new Dictionary<string, string?>();

            if (existing is not null)
            {
                var diff = new List<GuruImportDiffField>();
                void Track(string field, string? lama, string? baru)
                {
                    data[field] = baru;
                    if (lama != baru) diff.Add(new GuruImportDiffField { Field = field, Label = GuruImportFieldLabels.Map[field], Lama = lama, Baru = baru });
                }
                Track("nama", existing.Nama, nama);
                if (jkRaw != "") Track("jenis_kelamin", existing.JenisKelamin?.ToString(), jk);
                // jabatan ikut diubah HANYA kalau kolom Excel diisi - isPerusahaan
                // (selalu memaksa jabatan) tidak relevan di lingkup porting ini
                // (install_type "perusahaan" belum didukung) - §3.12.
                if (jabatanRaw != "") Track("jabatan", existing.Jabatan.ToString(), jabatan);
                if (gelar != "") Track("gelar_terakhir", existing.GelarTerakhir, gelar);

                preview.Rows.Add(new GuruImportPreviewRow { RowNumber = rowNum, NoHandphone = phone, Nama = nama, Type = "update", ExistingGuruId = existing.GuruId, Diff = diff, Data = data });
                preview.UpdateCount++;
            }
            else
            {
                if (!seenPhones.Add(phone))
                {
                    preview.Errors.Add($"Baris {rowNum}: No HP {phone} dipakai baris lain yang sama di file ini.");
                    preview.ErrorCount++; continue;
                }
                data["nama"] = nama;
                data["jenis_kelamin"] = jk;
                data["jabatan"] = jabatan;
                data["gelar_terakhir"] = gelar == "" ? null : gelar;
                data["no_handphone"] = phone;

                preview.Rows.Add(new GuruImportPreviewRow { RowNumber = rowNum, NoHandphone = phone, Nama = nama, Type = "insert", Data = data });
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
        return View("PreviewImport", JsonSerializer.Deserialize<GuruImportPreviewSession>(json)!);
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
        var preview = JsonSerializer.Deserialize<GuruImportPreviewSession>(json)!;

        await using var tx = await db.Database.BeginTransactionAsync();
        int inserted = 0, updated = 0;
        try
        {
            foreach (var row in preview.Rows)
            {
                if (row.Type == "insert")
                {
                    db.Guru.Add(new Data.Entities.Guru
                    {
                        Nama = row.Data["nama"]!,
                        Nip = null,
                        JenisKelamin = row.Data.GetValueOrDefault("jenis_kelamin") is { } jk ? Enum.Parse<JenisKelamin>(jk) : null,
                        Jabatan = Enum.Parse<JabatanGuru>(row.Data["jabatan"]!),
                        MataPelajaranCatatan = null,
                        GelarTerakhir = row.Data.GetValueOrDefault("gelar_terakhir"),
                        NoHandphone = row.Data["no_handphone"],
                        StatusAktif = true,
                    });
                    inserted++;
                }
                else
                {
                    var guru = await db.Guru.FindAsync(row.ExistingGuruId);
                    if (guru is null) continue;
                    if (row.Data.ContainsKey("nama")) guru.Nama = row.Data["nama"]!;
                    if (row.Data.ContainsKey("jenis_kelamin")) guru.JenisKelamin = row.Data["jenis_kelamin"] is { } jk ? Enum.Parse<JenisKelamin>(jk) : null;
                    if (row.Data.ContainsKey("jabatan")) guru.Jabatan = Enum.Parse<JabatanGuru>(row.Data["jabatan"]!);
                    if (row.Data.ContainsKey("gelar_terakhir")) guru.GelarTerakhir = row.Data["gelar_terakhir"];
                    // status_aktif/kelas TIDAK PERNAH disentuh lewat jalur import.
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

        var message = $"Berhasil import {inserted} guru baru.";
        if (updated > 0) message += $" {updated} guru lama diperbarui.";
        if (inserted > 0) message += " Lanjutkan ke menu \"Kelola Kelas\" utk menetapkan wali kelas, dan \"Guru Pengampu\" utk mata pelajaran resminya.";
        TempData["message"] = message;
        if (preview.Errors.Count > 0) TempData["import_errors"] = JsonSerializer.Serialize(preview.Errors);
        return RedirectToAction(nameof(Index));
    }

    // ---------------------------------------------------------------- Helper

    private static string? NormalizeJenisKelaminImport(string raw)
    {
        var v = raw.Trim().ToLowerInvariant();
        return v switch
        {
            "" => null,
            "l" or "laki-laki" or "laki laki" or "pria" => "L",
            "p" or "perempuan" or "wanita" => "P",
            _ => "invalid",
        };
    }

    private static string? NormalizeJabatanImport(string raw)
    {
        var v = raw.Trim().ToLowerInvariant();
        return v switch
        {
            "" => "guru_bidang",
            "guru" or "guru kelas" or "wali kelas" or "guru bidang" or "guru bidang studi" => "guru_bidang",
            "karyawan" or "pegawai" => "karyawan",
            _ => null,
        };
    }

    private static string NormalizeHeader(string h) => Regex.Replace(h.Trim(), @"\s+", " ").ToUpperInvariant();
    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string? OnlyDigits(string? s) => string.IsNullOrEmpty(s) ? s : Regex.Replace(s, @"\D+", "");
    private static bool IsNumericLike(string s) => Regex.IsMatch(s, @"^-?\d+(\.\d+)?$");

    private async Task<Dictionary<string, string>> ValidateFormAsync(GuruFormInput input, int? excludeId)
    {
        var e = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(input.Nip))
        {
            if (input.Nip.Length > 30) e["Nip"] = "NIP maksimal 30 karakter.";
            else if (!IsNumericLike(input.Nip)) e["Nip"] = "NIP hanya boleh berisi angka.";
            else if (await db.Guru.AnyAsync(g => g.Nip == input.Nip && g.GuruId != (excludeId ?? 0))) e["Nip"] = "NIP ini sudah dipakai guru/pegawai lain.";
        }

        var noHp = (input.NoHandphone ?? "").Trim();
        var digits = Regex.Replace(noHp, @"\D+", "");
        if (noHp == "") e["NoHandphone"] = "No. Handphone wajib diisi.";
        else if (noHp.Length > 20) e["NoHandphone"] = "No. Handphone maksimal 20 karakter.";
        else if (!IsNumericLike(digits)) e["NoHandphone"] = "No. Handphone hanya boleh berisi angka.";
        else if (await db.Guru.AnyAsync(g => g.NoHandphone == digits && g.StatusAktif && g.GuruId != (excludeId ?? 0)))
        {
            e["NoHandphone"] = "No. Handphone sudah digunakan guru/pegawai lain yang masih aktif.";
        }

        var nama = (input.Nama ?? "").Trim();
        if (nama == "") e["Nama"] = "Nama wajib diisi.";
        else if (nama.Length < 3) e["Nama"] = "Nama minimal 3 karakter.";
        else if (nama.Length > 100) e["Nama"] = "Nama maksimal 100 karakter.";
        else if (!Regex.IsMatch(nama, ValidationPatterns.RegexNama)) e["Nama"] = "Nama " + ValidationPatterns.PesanRegexNama;

        if (!string.IsNullOrEmpty(input.JenisKelamin) && input.JenisKelamin is not ("L" or "P"))
        {
            e["JenisKelamin"] = "Jenis kelamin tidak valid.";
        }

        if (input.Jabatan is not ("guru" or "karyawan")) e["Jabatan"] = "Jabatan wajib dipilih.";

        if (!string.IsNullOrWhiteSpace(input.MataPelajaran) && input.MataPelajaran.Length > 100) e["MataPelajaran"] = "Catatan mata pelajaran maksimal 100 karakter.";
        if (!string.IsNullOrWhiteSpace(input.GelarTerakhir) && input.GelarTerakhir.Length > 100) e["GelarTerakhir"] = "Gelar terakhir maksimal 100 karakter.";
        if (!string.IsNullOrWhiteSpace(input.Alamat) && input.Alamat.Length > 255) e["Alamat"] = "Alamat maksimal 255 karakter.";

        return e;
    }
}
