using DataMaster.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataMaster.Web.Controllers;

// Kelola PSB dari Data Master (2026-11-28): halaman pengantar + tombol yang meminta tiket sekali pakai lalu membuka PSB webview (gelombang, pendaftar,
// status, jadwal, pengumuman) di tab baru, sudah masuk otomatis dan terbatas pada katalog unit sekolah ini. Hak yang sama dengan menu Calon Siswa (admin).
[Authorize(Roles = "admin")]
[Route("kelola-psb")]
public class KelolaPsbController(PsbKelolaService layanan) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();

    [HttpPost("buka")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Buka(CancellationToken ct)
    {
        var hasil = await layanan.MintaAsync(User.Identity?.Name, ct);
        if (hasil.Url is null)
        {
            TempData["PsbGalat"] = hasil.Pesan;
            return RedirectToAction(nameof(Index));
        }
        return Redirect(hasil.Url);
    }
}
