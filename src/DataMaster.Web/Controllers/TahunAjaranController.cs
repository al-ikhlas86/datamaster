using System.Text.RegularExpressions;
using DataMaster.Data;
using DataMaster.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Controllers;

// Port 1:1 dari app/Controllers/TahunAjaran.php - lihat 02-guru-kelas-struktur.md §9.
[Route("tahun-ajaran")]
public class TahunAjaranController(DataMasterDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var list = await db.TahunAjaran.OrderByDescending(t => t.Nama).ToListAsync();
        return View(list);
    }

    [HttpPost("store")]
    public async Task<IActionResult> Store(string? nama)
    {
        var n = (nama ?? "").Trim();
        if (n == "" || !Regex.IsMatch(n, @"^\d{4}/\d{4}$"))
        {
            TempData["error"] = "Format tahun ajaran tidak valid (harus berupa angka dengan format YYYY/YYYY).";
            return RedirectToAction(nameof(Index));
        }
        if (await db.TahunAjaran.AnyAsync(t => t.Nama == n))
        {
            // PHP asli TIDAK menyediakan pesan custom utk aturan is_unique ini (hanya
            // regex_match yang dikustomisasi) - jatuh ke pesan default CI4 apa adanya,
            // sengaja dipertahankan meski jadi satu-satunya pesan berbahasa Inggris di
            // aplikasi ini. Lihat 02-guru-kelas-struktur.md §9.2.
            TempData["error"] = "The nama field must contain a unique value.";
            return RedirectToAction(nameof(Index));
        }

        // Tahun ajaran BARU SELALU dibuat NON-aktif - harus diaktifkan manual terpisah.
        db.TahunAjaran.Add(new TahunAjaran { Nama = n, IsActive = false });
        await db.SaveChangesAsync();

        TempData["message"] = "Tahun ajaran berhasil ditambahkan.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/set-active")]
    public async Task<IActionResult> SetActive(int id)
    {
        var ta = await db.TahunAjaran.FindAsync(id);
        if (ta is null)
        {
            TempData["error"] = "Tahun ajaran tidak ditemukan.";
            return RedirectToAction(nameof(Index));
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        // Nonaktifkan SEMUA baris dulu (tanpa where), baru aktifkan yang dipilih -
        // SENGAJA tidak memicu sinkronisasi jabatan guru sama sekali (lihat
        // 02-guru-kelas-struktur.md §12 poin 11 - perilaku APA ADANYA dari PHP asli).
        await db.TahunAjaran.ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, false));
        ta.IsActive = true;
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["message"] = $"Tahun ajaran {ta.Nama} berhasil diaktifkan.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var ta = await db.TahunAjaran.FindAsync(id);
        if (ta is null)
        {
            TempData["error"] = "Tahun ajaran tidak ditemukan.";
            return RedirectToAction(nameof(Index));
        }
        if (ta.IsActive)
        {
            TempData["error"] = "Tidak dapat menghapus tahun ajaran yang sedang aktif.";
            return RedirectToAction(nameof(Index));
        }

        // SENGAJA SELALU gagal, bahkan untuk tahun tidak aktif - tidak ada baris kode
        // yang benar-benar menghapus. Ini APA ADANYA dari PHP asli (arsip historis
        // permanen), BUKAN bug untuk "diperbaiki" - lihat §12 poin 12.
        TempData["error"] = $"Tahun ajaran {ta.Nama} dipertahankan sebagai arsip historis dan tidak dapat dihapus.";
        return RedirectToAction(nameof(Index));
    }
}
