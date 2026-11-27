using DataMaster.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataMaster.Web.Controllers;

// Kelola PSB dari Data Master (2026-11-28): halaman ini meminta tiket sekali pakai ke Hub lalu MENANAMKAN halaman PSB webview (gelombang, pendaftar, status, jadwal,
// pengumuman) langsung di dalam Data Master, sudah masuk otomatis dan terbatas pada katalog unit sekolah ini. Tampilannya sama dengan menu PSB di webview karena
// itu halaman yang sama. Setiap memuat halaman = tiket baru (tiket hanya berlaku sekali dan 2 menit). Hak yang sama dengan menu Calon Siswa (admin).
[Authorize(Roles = "admin")]
[Route("kelola-psb")]
public class KelolaPsbController(PsbKelolaService layanan) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var hasil = await layanan.MintaAsync(User.Identity?.Name, ct);
        ViewData["UrlPsb"] = hasil.Url;
        ViewData["PesanGalat"] = hasil.Pesan;
        return View();
    }
}
