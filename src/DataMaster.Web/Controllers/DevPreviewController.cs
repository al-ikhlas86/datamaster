using DataMaster.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DataMaster.Web.Controllers;

// Port 1:1 dari app/Controllers/DevPreview.php - lihat 04-infra-auth-sync.md §3.
// Guard SERVER-SIDE (bukan cuma disembunyikan di UI): install_type() harus literal
// "pengembang" - instalasi pendidikan/perusahaan asli TIDAK BISA mengakses ini
// meski request dikirim langsung ke endpoint.
[Route("dev-preview")]
public class DevPreviewController(InstallTypeService installType) : Controller
{
    [HttpPost("set")]
    public IActionResult Set(string? type)
    {
        if (installType.InstallType() != "pengembang")
        {
            TempData["error"] = "Fitur ini hanya tersedia untuk instalasi tipe pengembang.";
            return RedirectToAction("Index", "Home");
        }
        if (type is not ("pendidikan" or "perusahaan"))
        {
            TempData["error"] = "Tipe preview tidak valid.";
            return RedirectToAction("Index", "Home");
        }
        installType.SetDevPreview(type);
        return RedirectToAction("Index", "Home");
    }
}
