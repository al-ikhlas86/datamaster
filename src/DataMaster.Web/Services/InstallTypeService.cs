using Microsoft.Extensions.Options;

namespace DataMaster.Web.Services;

// Port dari app/Common.php: install_type()/effective_install_type()/
// is_pendidikan_module_active() - lihat 04-infra-auth-sync.md §5. "pengembang"
// BUKAN sebuah tampilan tersendiri - cuma status izin utk ganti-ganti preview
// tampilan Pendidikan/Perusahaan lewat DevPreviewController, TIDAK PERNAH
// dikembalikan oleh EffectiveInstallType().
public class InstallTypeService(IOptions<AppOptions> options, IHttpContextAccessor httpContextAccessor)
{
    private static readonly string[] ValidTypes = ["pendidikan", "perusahaan", "pengembang"];
    private const string SessionKey = "dev_preview_type";

    public string InstallType()
    {
        var v = (options.Value.InstallType ?? "").Trim().ToLowerInvariant();
        return ValidTypes.Contains(v) ? v : "pendidikan";
    }

    public string EffectiveInstallType()
    {
        var installType = InstallType();
        if (installType != "pengembang") return installType;

        var preview = httpContextAccessor.HttpContext?.Session.GetString(SessionKey);
        return preview is "pendidikan" or "perusahaan" ? preview : "pendidikan";
    }

    public bool IsPendidikanModuleActive() => EffectiveInstallType() == "pendidikan";

    public void SetDevPreview(string type) => httpContextAccessor.HttpContext?.Session.SetString(SessionKey, type);
}
