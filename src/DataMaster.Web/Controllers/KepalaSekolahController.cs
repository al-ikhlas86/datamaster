using DataMaster.Data.Entities;
using DataMaster.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataMaster.Data;

namespace DataMaster.Web.Controllers;

// Port 1:1 dari app/Controllers/KepalaSekolah.php - lihat 02-guru-kelas-struktur.md §6.
public class KepalaSekolahIndexViewModel
{
    // Ditemukan via audit "tidak ada BUG" (2026-09-08): sebelumnya Index() langsung
    // panggil kepsek.ActiveTahunAjaranIdAsync() TANPA proteksi - kalau database
    // sungguh2 kosong (instalasi baru, belum sempat bikin Tahun Ajaran), method itu
    // throw InvalidOperationException tak tertangani -> halaman crash 500 mentah ke
    // user. Modul lain (Dashboard, Akademik) sudah benar menangani kondisi ini
    // dgn state kosong yang ramah - KepalaSekolah SATU2NYA yang luput. Diperbaiki
    // dgn flag AdaTahunAjaran, pola sama persis DashboardPendidikanViewModel.
    public bool AdaTahunAjaran { get; set; }
    public KepalaSekolahSaatIni? SaatIni { get; set; }
    public int TahunAjaranId { get; set; }
    public int TahunAjaranAktif { get; set; }
    public List<(int Id, string Nama)> TahunAjaranList { get; set; } = [];
}

[Route("kepala-sekolah")]
public class KepalaSekolahController(DataMasterDbContext db, KepalaSekolahService kepsek) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? tahun)
    {
        var aktifId = await db.TahunAjaran.Where(t => t.IsActive).Select(t => t.TahunAjaranId).FirstOrDefaultAsync();
        var tahunAjaranList = await db.TahunAjaran.OrderByDescending(t => t.Nama).Select(t => new { t.TahunAjaranId, t.Nama }).ToListAsync();

        if (tahunAjaranList.Count == 0)
        {
            return View(new KepalaSekolahIndexViewModel { AdaTahunAjaran = false });
        }

        var taId = tahun is > 0 ? tahun.Value : (aktifId > 0 ? aktifId : tahunAjaranList[0].TahunAjaranId);

        var vm = new KepalaSekolahIndexViewModel
        {
            AdaTahunAjaran = true,
            SaatIni = await kepsek.GetSaatIniAsync(taId),
            TahunAjaranId = taId,
            TahunAjaranAktif = aktifId,
            TahunAjaranList = tahunAjaranList.Select(x => (x.TahunAjaranId, x.Nama)).ToList(),
        };
        return View(vm);
    }

    // TANPA filter "sudah jadi Kepala Sekolah" - kepala sekolah tidak eksklusif thd
    // jabatan lain (guru_kelas yang sedang jadi wali kelas TETAP boleh sekaligus jadi
    // kepala sekolah). Kandidat SEMUA jabatan (guru_kelas/guru_bidang/karyawan) - §6.2.
    [HttpGet("cari-kandidat")]
    public async Task<IActionResult> CariKandidat(string? q)
    {
        var kata = (q ?? "").Trim();
        var semua = await db.Guru.Where(g => g.StatusAktif).OrderBy(g => g.Nama).ToListAsync();
        var hasil = semua
            .Where(g => kata == "" || g.Nama.Contains(kata, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .Select(g => new { guru_id = g.GuruId, nama = g.Nama, jabatan = g.Jabatan.ToString() })
            .ToList();
        return Json(hasil);
    }

    [HttpPost("tetapkan")]
    public async Task<IActionResult> Tetapkan(int guru_id, int? tahun_ajaran_id)
    {
        var ta = tahun_ajaran_id ?? await db.TahunAjaran.Where(t => t.IsActive).Select(t => t.TahunAjaranId).FirstOrDefaultAsync();
        if (ta <= 0)
        {
            TempData["error"] = "Tahun ajaran belum dipilih/aktif.";
            return RedirectToAction(nameof(Index));
        }
        var guru = await db.Guru.FindAsync(guru_id);
        if (guru is null || !guru.StatusAktif)
        {
            TempData["error"] = "Guru/pegawai tidak ditemukan atau tidak aktif.";
            return Redirect(Request.Headers.Referer.ToString() is { Length: > 0 } r ? r : Url.Action(nameof(Index))!);
        }

        await kepsek.TetapkanAsync(guru_id, ta);
        TempData["message"] = $"{guru.Nama} ditetapkan sebagai Kepala Sekolah.";
        return RedirectToAction(nameof(Index), new { tahun = ta });
    }

    [HttpPost("kosongkan")]
    public async Task<IActionResult> Kosongkan(int? tahun_ajaran_id)
    {
        var ta = tahun_ajaran_id ?? await db.TahunAjaran.Where(t => t.IsActive).Select(t => t.TahunAjaranId).FirstOrDefaultAsync();
        if (ta <= 0)
        {
            TempData["error"] = "Tahun ajaran belum dipilih/aktif.";
            return RedirectToAction(nameof(Index));
        }
        await kepsek.KosongkanAsync(ta);
        TempData["message"] = "Kepala Sekolah dikosongkan.";
        return RedirectToAction(nameof(Index), new { tahun = ta });
    }
}
