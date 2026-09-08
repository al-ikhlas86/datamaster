using DataMaster.Data;
using DataMaster.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Controllers;

// Port SEBAGIAN dari app/Controllers/Kurikulum.php - HANYA 3 method Master Tingkat
// (storeTingkat/updateTingkatBatch/deleteTingkat). Modul Kurikulum lengkap (Mata
// Pelajaran, Alokasi JP, Jam Belajar) BELUM diporting - lihat 03-akademik-jadwal.md §3.
// Route SENGAJA tetap di bawah "kurikulum/tingkat" walau UI-nya dipasang di halaman
// Kelola Kelas (persis PHP asli sejak 2026-08-27, lihat §10 poin I di spec yang sama).
[Route("kurikulum/tingkat")]
public class KurikulumController(DataMasterDbContext db) : Controller
{
    // "kembali_ke" HANYA menerima literal "kelas" (whitelist anti-open-redirect) -
    // satu-satunya konsumen saat ini adalah panel Tingkat di halaman Kelas.
    private IActionResult TujuanTingkat(string? kembaliKe)
    {
        return kembaliKe == "kelas" ? RedirectToAction("Index", "Kelas") : RedirectToAction("Index", "Kelas");
    }

    [HttpPost("store")]
    public async Task<IActionResult> Store(string? kode, string? nama, int urutan, string? kembali_ke)
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
            return TujuanTingkat(kembali_ke);
        }

        db.Tingkat.Add(new Tingkat { Kode = k, Nama = n, Urutan = urutan, IsActive = true });
        await db.SaveChangesAsync();
        TempData["message"] = $"Tingkat \"{n}\" ditambahkan.";
        return TujuanTingkat(kembali_ke);
    }

    [HttpPost("update")]
    public async Task<IActionResult> UpdateBatch([FromForm] Dictionary<string, TingkatRowInput> rows, [FromForm(Name = "is_active")] List<int>? isActive, string? kembali_ke)
    {
        var aktifSet = (isActive ?? []).ToHashSet();
        var ok = 0;
        foreach (var (idStr, row) in rows)
        {
            if (!int.TryParse(idStr, out var id)) continue;
            var t = await db.Tingkat.FindAsync(id);
            if (t is null) continue;
            // Kode SENGAJA TIDAK ikut diubah - dipakai kunci relasi teks di kelas.tingkat
            // dll (lihat komentar entity Tingkat.cs). Hanya nama/urutan/is_active bebas diubah.
            t.Nama = (row.Nama ?? t.Nama).Trim();
            t.Urutan = row.Urutan;
            t.IsActive = aktifSet.Contains(id);
            ok++;
        }
        await db.SaveChangesAsync();
        TempData["message"] = $"{ok} tingkat diperbarui.";
        return TujuanTingkat(kembali_ke);
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, string? kembali_ke)
    {
        var t = await db.Tingkat.FindAsync(id);
        if (t is null)
        {
            TempData["error"] = "Tingkat tidak ditemukan.";
            return TujuanTingkat(kembali_ke);
        }

        // Tidak ada FK constraint DB (relasi via kode teks) - dihitung MANUAL, sama
        // seperti PHP asli (§10 poin I).
        var dipakai = await db.Kelas.CountAsync(k => k.Tingkat == t.Kode);
        if (dipakai > 0)
        {
            TempData["error"] = $"Tidak bisa menghapus \"{t.Nama}\" - masih dipakai {dipakai} kelas. Nonaktifkan saja bila sudah tidak dipakai.";
            return TujuanTingkat(kembali_ke);
        }

        db.Tingkat.Remove(t);
        await db.SaveChangesAsync();
        TempData["message"] = $"Tingkat \"{t.Nama}\" dihapus.";
        return TujuanTingkat(kembali_ke);
    }

    // Route absolut (bukan di bawah "kurikulum/tingkat") - tambah cepat Mata Pelajaran
    // dipakai oleh halaman Guru Pengampu selama modul Kurikulum lengkap (Mata Pelajaran/
    // Alokasi JP/Jam Belajar) belum diporting - lihat 03-akademik-jadwal.md §3 TAB1 & §5.1.
    [HttpPost("/kurikulum/mata-pelajaran/store")]
    public async Task<IActionResult> StoreMapel(string? kode, string? nama, string? kelompok, int urutan, string? kembali_ke)
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
            return RedirectBack(kembali_ke);
        }

        db.MataPelajaran.Add(new MataPelajaran { Nama = n, Kode = k, Kelompok = kel, Urutan = urutan });
        await db.SaveChangesAsync();
        TempData["message"] = $"Mata pelajaran \"{n}\" ditambahkan.";
        return RedirectBack(kembali_ke);
    }

    private IActionResult RedirectBack(string? kembaliKe) =>
        kembaliKe == "penugasan-mengajar" ? RedirectToAction("Index", "PenugasanMengajar") : RedirectToAction("Index", "PenugasanMengajar");
}

public class TingkatRowInput
{
    public string? Nama { get; set; }
    public int Urutan { get; set; }
}
