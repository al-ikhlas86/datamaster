using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DataMaster.Data;
using DataMaster.Web.Models.KalenderAkademik;
using DataMaster.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Controllers;

// Port 1:1 dari app/Controllers/KalenderAkademik.php - lihat 03-akademik-jadwal.md §6-7.
[Route("kalender-akademik")]
public class KalenderAkademikController(DataMasterDbContext db) : Controller
{
    private static readonly Dictionary<string, string> WarnaSaranDefault = new()
    {
        ["Libur Nasional"] = "#dc3545",
        ["Libur Semester"] = "#dc3545",
        ["Asesmen"] = "#0d6efd",
        ["Kegiatan"] = "#198754",
        ["Rapor"] = "#fd7e14",
        ["Ujian Kelas 6"] = "#6f42c1",
    };

    private async Task<int> ResolveTahunAjaranIdAsync(int? tahun)
    {
        if (tahun is > 0) return tahun.Value;
        var aktif = await db.TahunAjaran.FirstOrDefaultAsync(t => t.IsActive);
        return aktif?.TahunAjaranId ?? 0;
    }

    // Rentang tanggal WAJAR 1 tahun ajaran, buffer ~1 bulan tiap sisi - dipakai
    // memvalidasi input/import supaya tanggal yg TAHUNNYA KELIRU tertangkap SAAT ITU
    // JUGA. Nama TA WAJIB format "YYYY/YYYY" - kalau tidak, validasi DILEWATI (null).
    // Lihat 03-akademik-jadwal.md §6.
    private async Task<(string Nama, DateOnly Awal, DateOnly Akhir)?> RentangWajarAsync(int tahunAjaranId)
    {
        var ta = await db.TahunAjaran.FindAsync(tahunAjaranId);
        if (ta is null) return null;
        var m = Regex.Match(ta.Nama, @"^(\d{4})/(\d{4})$");
        if (!m.Success) return null;
        var tahun1 = int.Parse(m.Groups[1].Value);
        var tahun2 = int.Parse(m.Groups[2].Value);
        return (ta.Nama, new DateOnly(tahun1, 6, 1), new DateOnly(tahun2, 7, 31));
    }

    private static bool DiLuarRentangWajar(DateOnly tanggal, (string Nama, DateOnly Awal, DateOnly Akhir)? rentang) =>
        rentang is not null && (tanggal < rentang.Value.Awal || tanggal > rentang.Value.Akhir);

    [HttpGet("")]
    public async Task<IActionResult> Index(int? tahun)
    {
        var taId = await ResolveTahunAjaranIdAsync(tahun);
        var tahunAjaranList = await db.TahunAjaran.OrderByDescending(t => t.Nama).Select(t => new { t.TahunAjaranId, t.Nama }).ToListAsync();

        var agenda = await db.KalenderAkademik.Where(k => k.TahunAjaranId == taId)
            .OrderBy(k => k.TanggalMulai)
            .Select(k => new AgendaRow
            {
                KalenderAkademikId = k.KalenderAkademikId,
                Judul = k.Judul,
                Kategori = k.Kategori,
                Warna = k.Warna,
                TanggalMulai = k.TanggalMulai,
                TanggalSelesai = k.TanggalSelesai,
                Waktu = k.Waktu,
                Sasaran = k.Sasaran,
                IsLibur = k.IsLibur,
                Keterangan = k.Keterangan,
            }).ToListAsync();

        var kategoriTerpakai = agenda.Where(a => !string.IsNullOrEmpty(a.Kategori)).Select(a => a.Kategori!).Distinct();
        var kategoriList = kategoriTerpakai.Union(WarnaSaranDefault.Keys).OrderBy(k => k).ToList();

        var vm = new KalenderIndexViewModel
        {
            TahunAjaranId = taId,
            TahunAjaranList = tahunAjaranList.Select(x => (x.TahunAjaranId, x.Nama)).ToList(),
            Agenda = agenda,
            KategoriList = kategoriList,
            WarnaSaran = WarnaSaranDefault,
        };
        return View(vm);
    }

    [HttpPost("store")]
    public async Task<IActionResult> Store(int tahun_ajaran_id, string? judul, string? kategori, string? warna,
        DateOnly? tanggal_mulai, DateOnly? tanggal_selesai, string? waktu, string? sasaran, bool is_libur, string? keterangan)
    {
        if (tahun_ajaran_id <= 0)
        {
            TempData["error"] = "Tahun ajaran belum dipilih.";
            return RedirectToAction(nameof(Index));
        }
        var j = (judul ?? "").Trim();
        if (j == "" || j.Length > 200)
        {
            TempData["error"] = "Nama kegiatan wajib diisi.";
            return RedirectToAction(nameof(Index), new { tahun = tahun_ajaran_id });
        }
        if (tanggal_mulai is null)
        {
            TempData["error"] = "Tanggal mulai wajib diisi.";
            return RedirectToAction(nameof(Index), new { tahun = tahun_ajaran_id });
        }
        if (tanggal_selesai is not null && tanggal_selesai < tanggal_mulai)
        {
            TempData["error"] = "Tanggal selesai tidak boleh sebelum tanggal mulai.";
            return RedirectToAction(nameof(Index), new { tahun = tahun_ajaran_id });
        }

        var rentang = await RentangWajarAsync(tahun_ajaran_id);
        if (DiLuarRentangWajar(tanggal_mulai.Value, rentang))
        {
            TempData["error"] = $"Tanggal mulai ({tanggal_mulai:yyyy-MM-dd}) di luar rentang wajar tahun ajaran {rentang!.Value.Nama}. Periksa lagi - mungkin tahunnya salah ketik.";
            return RedirectToAction(nameof(Index), new { tahun = tahun_ajaran_id });
        }

        db.KalenderAkademik.Add(new Data.Entities.KalenderAkademik
        {
            TahunAjaranId = tahun_ajaran_id,
            Judul = j,
            Kategori = NullIfEmpty(kategori),
            Warna = NullIfEmpty(warna),
            TanggalMulai = tanggal_mulai.Value,
            TanggalSelesai = tanggal_selesai,
            Waktu = NullIfEmpty(waktu),
            Sasaran = NullIfEmpty(sasaran),
            IsLibur = is_libur,
            Keterangan = NullIfEmpty(keterangan),
        });
        await db.SaveChangesAsync();

        TempData["message"] = $"Agenda \"{j}\" ditambahkan.";
        return RedirectToAction(nameof(Index), new { tahun = tahun_ajaran_id });
    }

    [HttpPost("{id:int}/update")]
    public async Task<IActionResult> Update(int id, int tahun_ajaran_id, string? judul, string? kategori, string? warna,
        DateOnly? tanggal_mulai, DateOnly? tanggal_selesai, string? waktu, string? sasaran, bool is_libur, string? keterangan)
    {
        var agenda = await db.KalenderAkademik.FindAsync(id);
        if (agenda is null)
        {
            TempData["error"] = "Agenda tidak ditemukan.";
            return RedirectToAction(nameof(Index), new { tahun = tahun_ajaran_id });
        }
        if (tahun_ajaran_id <= 0)
        {
            TempData["error"] = "Tahun ajaran belum dipilih.";
            return RedirectToAction(nameof(Index));
        }
        var j = (judul ?? "").Trim();
        if (j == "" || j.Length > 200)
        {
            TempData["error"] = "Nama kegiatan wajib diisi.";
            return RedirectToAction(nameof(Index), new { tahun = tahun_ajaran_id });
        }
        if (tanggal_mulai is null)
        {
            TempData["error"] = "Tanggal mulai wajib diisi.";
            return RedirectToAction(nameof(Index), new { tahun = tahun_ajaran_id });
        }
        if (tanggal_selesai is not null && tanggal_selesai < tanggal_mulai)
        {
            TempData["error"] = "Tanggal selesai tidak boleh sebelum tanggal mulai.";
            return RedirectToAction(nameof(Index), new { tahun = tahun_ajaran_id });
        }
        var rentang = await RentangWajarAsync(tahun_ajaran_id);
        if (DiLuarRentangWajar(tanggal_mulai.Value, rentang))
        {
            TempData["error"] = $"Tanggal mulai ({tanggal_mulai:yyyy-MM-dd}) di luar rentang wajar tahun ajaran {rentang!.Value.Nama}. Periksa lagi - mungkin tahunnya salah ketik.";
            return RedirectToAction(nameof(Index), new { tahun = tahun_ajaran_id });
        }

        agenda.Judul = j;
        agenda.Kategori = NullIfEmpty(kategori);
        agenda.Warna = NullIfEmpty(warna);
        agenda.TanggalMulai = tanggal_mulai.Value;
        agenda.TanggalSelesai = tanggal_selesai;
        agenda.Waktu = NullIfEmpty(waktu);
        agenda.Sasaran = NullIfEmpty(sasaran);
        agenda.IsLibur = is_libur;
        agenda.Keterangan = NullIfEmpty(keterangan);
        await db.SaveChangesAsync();

        TempData["message"] = "Agenda diperbarui.";
        return RedirectToAction(nameof(Index), new { tahun = tahun_ajaran_id });
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, int? tahun_ajaran_id)
    {
        var agenda = await db.KalenderAkademik.FindAsync(id);
        if (agenda is null)
        {
            TempData["error"] = "Agenda tidak ditemukan.";
            return RedirectToAction(nameof(Index), new { tahun = tahun_ajaran_id });
        }
        var judul = agenda.Judul;
        var ta = agenda.TahunAjaranId;
        db.KalenderAkademik.Remove(agenda);
        await db.SaveChangesAsync();

        TempData["message"] = $"Agenda \"{judul}\" dihapus.";
        return RedirectToAction(nameof(Index), new { tahun = ta });
    }

    // ------------------------------------------------------------- Import

    [HttpGet("import")]
    public async Task<IActionResult> Import(int? tahun)
    {
        var taId = await ResolveTahunAjaranIdAsync(tahun);
        var ta = await db.TahunAjaran.FindAsync(taId);
        ViewBag.TahunAjaranId = taId;
        ViewBag.TahunAjaranNama = ta?.Nama ?? "-";
        return View();
    }

    [HttpGet("download-template")]
    public IActionResult DownloadTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Kalender");
        var headers = new[] { "Tanggal", "Tanggal Selesai (opsional)", "Kegiatan", "Kategori", "Waktu", "Sasaran", "Libur", "Keterangan" };
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];

        var rows = new[]
        {
            new[] { "13-17 Juli 2026", "", "Masuk Pertama / Ta'aruf / MPLS", "Kegiatan", "08.00 - 11.00", "Kelas 1-6", "tidak", "" },
            new[] { "17 Agustus 2026", "", "HUT RI", "Libur Nasional", "", "", "ya", "" },
            new[] { "18 & 25 Juli 2026", "", "Silaturahmi wali murid", "Kegiatan", "", "Kelas 1-6", "tidak", "Ditulis pakai \"&\" = 2 acara terpisah" },
            new[] { "21 Des - 02 Jan 2027", "", "Libur Semester Ganjil", "Libur Semester", "", "Kelas 1-6", "ya", "" },
            new[] { "2026-09-14", "2026-09-22", "Asesmen Sumatif Tengah Semester", "Asesmen", "08.00 - 11.00", "Kelas 1-6", "tidak", "Format lama 2 kolom tetap bisa" },
        };
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = rows[r][c];

        var panduan = wb.Worksheets.Add("Panduan");
        panduan.Cell(1, 1).Value = @"CARA MENGISI

Kolom ""Tanggal"" boleh ditulis seperti di kalender sekolah - tidak perlu dipecah:
    10 Juli 2026            -> satu hari
    13-17 Juli 2026         -> 13 sampai 17 Juli
    25 Mar-03 April 2027    -> lintas bulan
    21 Des - 02 Jan 2027    -> lintas tahun (Desember otomatis dibaca 2026)
    18 & 25 Juli 2026       -> DUA acara terpisah, masing-masing 1 hari
    2026-07-13              -> format lama, tetap diterima

Tanggal Selesai : boleh dikosongkan. Isi hanya kalau memakai format lama 2 kolom.
Kegiatan        : wajib, nama kegiatan.
Kategori        : bebas diisi (Libur Nasional, Asesmen, Kegiatan, Rapor, dll).
Waktu           : bebas, contoh ""08.00 - 11.00"".
Sasaran         : untuk siapa, contoh ""Kelas 6"" atau ""Kelas 1-6"".
Libur           : isi ""ya"" jika hari itu tidak ada kegiatan belajar, selain itu ""tidak"".
Keterangan      : catatan tambahan, boleh dikosongkan.

YANG AKAN DILEWATI DAN DILAPORKAN (bukan ditebak):
    Okt/Nov 2026          - tidak ada tanggalnya
    27-05 Juni 2027       - tanggal awal lebih besar dari akhir di bulan yang sama
    21 Des 25 - 02 Jan 26 - tahun ditulis 2 digit, tidak jelas tahunnya
Perbaiki penulisannya lalu import ulang - agenda yang sudah masuk tidak akan terduplikat.";

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "template_kalender_akademik.xlsx");
    }

    [HttpPost("process-import")]
    public async Task<IActionResult> ProcessImport(IFormFile? file, int tahun_ajaran_id)
    {
        if (tahun_ajaran_id <= 0)
        {
            // PHP asli: redirect()->to(base_url('kalender-akademik')) - balik ke INDEX,
            // BUKAN ke halaman import lagi (beda dari validasi lain di bawah yang balik
            // ke Import). Lihat KalenderAkademik::processImport() baris awal.
            TempData["error"] = "Tahun ajaran belum dipilih.";
            return RedirectToAction(nameof(Index));
        }
        if (file is null || file.Length == 0)
        {
            TempData["error"] = "File tidak ditemukan.";
            return RedirectToAction(nameof(Import), new { tahun = tahun_ajaran_id });
        }
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
        {
            TempData["error"] = "Format file harus .xlsx atau .xls.";
            return RedirectToAction(nameof(Import), new { tahun = tahun_ajaran_id });
        }
        if (file.Length > 5 * 1024 * 1024)
        {
            TempData["error"] = "Ukuran file terlalu besar. Maksimal 5 MB.";
            return RedirectToAction(nameof(Import), new { tahun = tahun_ajaran_id });
        }

        List<string[]> rows;
        try
        {
            using var stream = file.OpenReadStream();
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();
            rows = ws.RowsUsed().Select(row => Enumerable.Range(1, 8).Select(i => row.Cell(i).GetString().Trim()).ToArray()).ToList();
        }
        catch (Exception ex)
        {
            TempData["error"] = $"Error: {ex.Message}";
            return RedirectToAction(nameof(Import), new { tahun = tahun_ajaran_id });
        }

        var rentang = await RentangWajarAsync(tahun_ajaran_id);
        int masuk = 0, duplikat = 0, dilewati = 0;
        var errors = new List<string>();

        for (var r = 1; r < rows.Count; r++)
        {
            var rowNum = r + 1;
            var cells = rows[r];
            var tglMentah = cells[0];
            var tglSelesaiMentah = cells[1];
            var judul = cells[2];
            var kategori = cells[3];
            var waktu = cells[4];
            var sasaran = cells[5];
            var libur = cells[6];
            var keterangan = cells[7];

            if (tglMentah == "" && judul == "") continue;
            if (judul == "") { errors.Add($"Baris {rowNum}: nama kegiatan wajib diisi."); dilewati++; continue; }

            List<(DateOnly Mulai, DateOnly? Selesai)> periode;
            if (tglSelesaiMentah != "")
            {
                var mulai = IndonesianDateService.ParseIndonesianDate(tglMentah);
                if (mulai is null) { errors.Add($"Baris {rowNum}: tanggal mulai \"{tglMentah}\" tidak dikenali."); dilewati++; continue; }
                var selesai = IndonesianDateService.ParseIndonesianDate(tglSelesaiMentah);
                if (selesai is not null && selesai < mulai) { errors.Add($"Baris {rowNum}: tanggal selesai sebelum tanggal mulai."); dilewati++; continue; }
                periode = [(mulai.Value, selesai)];
            }
            else
            {
                var hasil = IndonesianDateService.ParseIndonesianDateRange(tglMentah);
                if (hasil.Count == 0) { errors.Add($"Baris {rowNum}: tanggal \"{tglMentah}\" tidak dikenali atau ambigu - perbaiki penulisannya lalu import ulang."); dilewati++; continue; }
                periode = hasil.Select(p => (p.Mulai, p.Selesai)).ToList();
            }

            var dasar = new
            {
                Judul = judul,
                Kategori = kategori == "" ? null : kategori,
                Warna = kategori != "" && WarnaSaranDefault.TryGetValue(kategori, out var w) ? w : null,
                Waktu = waktu == "" ? null : waktu,
                Sasaran = sasaran == "" ? null : sasaran,
                IsLibur = new[] { "ya", "y", "1", "true" }.Contains(libur.Trim().ToLowerInvariant()),
                Keterangan = keterangan == "" ? null : keterangan,
            };

            foreach (var p in periode)
            {
                if (DiLuarRentangWajar(p.Mulai, rentang))
                {
                    errors.Add($"Baris {rowNum}: tanggal \"{tglMentah}\" ({p.Mulai:yyyy-MM-dd}) di luar rentang wajar tahun ajaran {rentang!.Value.Nama} - kemungkinan tahunnya salah ketik.");
                    dilewati++;
                    continue;
                }
                var sudahAda = await db.KalenderAkademik.AnyAsync(k => k.TahunAjaranId == tahun_ajaran_id && k.Judul == dasar.Judul && k.TanggalMulai == p.Mulai);
                if (sudahAda) { duplikat++; continue; }

                db.KalenderAkademik.Add(new Data.Entities.KalenderAkademik
                {
                    TahunAjaranId = tahun_ajaran_id,
                    Judul = dasar.Judul,
                    Kategori = dasar.Kategori,
                    Warna = dasar.Warna,
                    TanggalMulai = p.Mulai,
                    TanggalSelesai = p.Selesai,
                    Waktu = dasar.Waktu,
                    Sasaran = dasar.Sasaran,
                    IsLibur = dasar.IsLibur,
                    Keterangan = dasar.Keterangan,
                });
                masuk++;
            }
        }
        await db.SaveChangesAsync();

        var message = $"Import selesai: {masuk} agenda masuk";
        if (duplikat > 0) message += $", {duplikat} sudah ada (dilewati)";
        message += $", {dilewati} tidak terbaca.";
        TempData["message"] = message;
        if (errors.Count > 0) TempData["import_errors"] = System.Text.Json.JsonSerializer.Serialize(errors);
        return RedirectToAction(nameof(Index), new { tahun = tahun_ajaran_id });
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
