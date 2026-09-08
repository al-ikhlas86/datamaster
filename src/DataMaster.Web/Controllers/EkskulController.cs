using DataMaster.Data;
using DataMaster.Data.Entities;
using DataMaster.Web.Models.Ekskul;
using DataMaster.Web.Models.Kelas;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Controllers;

// Port 1:1 dari app/Controllers/Ekskul.php - lihat 02-guru-kelas-struktur.md §10.
[Route("ekskul")]
public class EkskulController(DataMasterDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        // getEkskulWithCount(): total_peserta = COUNT SEMUA baris ekskul_siswa TANPA
        // filter status siswa - SENGAJA beda dari getPesertaByEkskul() (detail) yang
        // filter status='aktif' - lihat §12 poin 20 (edge-case, bukan bug).
        var rows = await db.Ekskul
            .Select(e => new EkskulRow
            {
                EkskulId = e.EkskulId,
                Nama = e.Nama,
                Pembina = e.Pembina,
                Hari = e.Hari,
                TotalPeserta = e.EkskulSiswaList.Count,
                IsActive = e.IsActive,
            })
            .OrderByDescending(e => e.IsActive).ThenBy(e => e.Nama)
            .ToListAsync();

        var vm = new EkskulIndexViewModel
        {
            Rows = rows,
            TotalAktif = rows.Count(r => r.IsActive),
            TotalPesertaSemua = rows.Sum(r => r.TotalPeserta),
        };
        return View(vm);
    }

    [HttpPost("store")]
    public async Task<IActionResult> Store(string? nama, string? pembina, string? hari, string? deskripsi)
    {
        var n = (nama ?? "").Trim();
        if (n == "" || n.Length < 3 || n.Length > 100)
        {
            TempData["error"] = "Nama ekskul wajib diisi (minimal 3, maksimal 100 karakter).";
            return RedirectToAction(nameof(Index));
        }

        db.Ekskul.Add(new Data.Entities.Ekskul
        {
            Nama = n,
            Pembina = NullIfEmpty(pembina),
            Hari = NullIfEmpty(hari),
            Deskripsi = NullIfEmpty(deskripsi),
            IsActive = true,
        });
        await db.SaveChangesAsync();

        TempData["message"] = "Ekskul baru berhasil ditambahkan.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var e = await db.Ekskul.FindAsync(id);
        if (e is null) return NotFound("Ekskul tidak ditemukan.");
        return View(e);
    }

    [HttpPost("{id:int}/update")]
    public async Task<IActionResult> Update(int id, string? nama, string? pembina, string? hari, string? deskripsi)
    {
        var e = await db.Ekskul.FindAsync(id);
        if (e is null) return NotFound("Ekskul tidak ditemukan.");

        var n = (nama ?? "").Trim();
        if (n == "" || n.Length < 3 || n.Length > 100)
        {
            ModelState.AddModelError("nama", "Nama ekskul wajib diisi (minimal 3, maksimal 100 karakter).");
            return View("Edit", e);
        }

        // is_active SENGAJA tidak disentuh di sini - hanya lewat archive/restore terpisah.
        e.Nama = n;
        e.Pembina = NullIfEmpty(pembina);
        e.Hari = NullIfEmpty(hari);
        e.Deskripsi = NullIfEmpty(deskripsi);
        await db.SaveChangesAsync();

        TempData["message"] = "Data ekskul berhasil diperbarui.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/archive")]
    public async Task<IActionResult> Archive(int id)
    {
        var e = await db.Ekskul.FindAsync(id);
        if (e is null)
        {
            TempData["error"] = "Ekskul tidak ditemukan.";
            return RedirectToAction(nameof(Index));
        }
        // Riwayat peserta tetap utuh (tidak dihapus).
        e.IsActive = false;
        e.ArchivedAt = DateTime.Now;
        await db.SaveChangesAsync();

        TempData["message"] = "Ekskul dipindahkan ke arsip.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var e = await db.Ekskul.FindAsync(id);
        if (e is null)
        {
            TempData["error"] = "Ekskul tidak ditemukan.";
            return RedirectToAction(nameof(Index));
        }
        e.IsActive = true;
        e.ArchivedAt = null;
        await db.SaveChangesAsync();

        TempData["message"] = "Ekskul diaktifkan kembali.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var e = await db.Ekskul.FindAsync(id);
        if (e is null) return NotFound("Ekskul tidak ditemukan.");

        var vm = new EkskulDetailViewModel
        {
            EkskulId = e.EkskulId,
            Nama = e.Nama,
            Pembina = e.Pembina,
            Hari = e.Hari,
            Deskripsi = e.Deskripsi,
            Peserta = await db.EkskulSiswa.Where(es => es.EkskulId == id && es.Siswa.Status == StatusSiswa.aktif)
                .OrderBy(es => es.Siswa.Nama)
                .Select(es => new EkskulPesertaRow { SiswaId = es.SiswaId, Nama = es.Siswa.Nama, Nis = es.Siswa.Nis, NamaKelas = es.Siswa.Kelas != null ? es.Siswa.Kelas.NamaKelas : null, TanggalDaftar = es.TanggalDaftar })
                .ToListAsync(),
            SiswaBelumIkut = await db.Siswa.Where(s => s.Status == StatusSiswa.aktif && !db.EkskulSiswa.Any(es => es.EkskulId == id && es.SiswaId == s.SiswaId))
                .OrderBy(s => s.Nama)
                .Select(s => new SiswaRingkas { SiswaId = s.SiswaId, Nama = s.Nama, Nis = s.Nis, JenisKelamin = s.JenisKelamin.ToString() })
                .ToListAsync(),
        };
        return View(vm);
    }

    [HttpPost("{id:int}/add-siswa")]
    public async Task<IActionResult> AddSiswa(int id, List<int>? siswa_ids)
    {
        var e = await db.Ekskul.FindAsync(id);
        if (e is null)
        {
            TempData["error"] = "Ekskul tidak ditemukan.";
            return RedirectToAction(nameof(Index));
        }
        var ids = siswa_ids ?? [];
        if (ids.Count == 0)
        {
            TempData["warning"] = "Tidak ada siswa yang dipilih.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        // Cek dulu apakah SUDAH terdaftar sebelum insert - SENGAJA (bukan andalkan
        // UNIQUE-key exception) supaya form disubmit dobel/lambat tidak berhenti di
        // tengah loop - lihat §12 poin 21.
        var today = DateOnly.FromDateTime(DateTime.Now);
        foreach (var siswaId in ids)
        {
            var sudahAda = await db.EkskulSiswa.AnyAsync(es => es.EkskulId == id && es.SiswaId == siswaId);
            if (!sudahAda)
            {
                db.EkskulSiswa.Add(new EkskulSiswa { EkskulId = id, SiswaId = siswaId, TanggalDaftar = today, CreatedAt = DateTime.Now });
            }
        }
        await db.SaveChangesAsync();

        // Pesan melaporkan JUMLAH YANG DICENTANG, bukan jumlah yang benar-benar diinsert.
        TempData["message"] = $"{ids.Count} siswa berhasil ditambahkan sebagai peserta.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{ekskulId:int}/remove/{siswaId:int}")]
    public async Task<IActionResult> RemoveSiswa(int ekskulId, int siswaId)
    {
        await db.EkskulSiswa.Where(es => es.EkskulId == ekskulId && es.SiswaId == siswaId).ExecuteDeleteAsync();
        TempData["message"] = "Siswa berhasil dikeluarkan dari ekskul.";
        return RedirectToAction(nameof(Detail), new { id = ekskulId });
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
