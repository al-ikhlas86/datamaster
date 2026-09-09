using System.Text.Json;
using System.Text.Json.Nodes;

namespace DataMaster.Web.Services;

// Menulis balik ke appsettings.json (bagian "AppSettings" saja, field lain
// dibiarkan utuh) - dipakai supaya Setup Awal & menu Pengaturan bisa mengisi
// HubApiUrl/HubApiToken TANPA staf IT perlu buka file manual (lihat diskusi
// "user dipermudah, jangan disuruh edit file sendiri"). IHostApplicationLifetime
// dipanggil TERPISAH oleh caller setelah menulis (bukan di sini) - supaya proses
// server di-restart Launcher & config baru kebaca ulang sejak proses fresh
// (IOptions<AppOptions> singleton, TIDAK hot-reload seperti IOptionsMonitor).
public class AppSettingsWriterService(IWebHostEnvironment env, ILogger<AppSettingsWriterService> logger)
{
    private string PathAppSettings => Path.Combine(env.ContentRootPath, "appsettings.json");

    public async Task SetHubApiConfigAsync(string? hubApiUrl, string? hubApiToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(PathAppSettings);
            var root = JsonNode.Parse(json)?.AsObject() ?? new JsonObject();

            if (root["AppSettings"] is not JsonObject appSettings)
            {
                appSettings = new JsonObject();
                root["AppSettings"] = appSettings;
            }

            if (hubApiUrl is not null) appSettings["HubApiUrl"] = hubApiUrl.Trim();
            if (hubApiToken is not null) appSettings["HubApiToken"] = hubApiToken.Trim();

            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(PathAppSettings, root.ToJsonString(options));
        }
        catch (Exception ex)
        {
            // Non-fatal SENGAJA - kalau gagal tulis (mis. file readonly/terkunci),
            // akun admin tetap berhasil dibuat, cuma sinkron Hub API belum aktif -
            // bisa dicoba lagi manual lewat menu Pengaturan, jangan sampai gagal
            // tulis config ikut menggagalkan Setup Awal sepenuhnya.
            logger.LogWarning(ex, "Gagal menulis konfigurasi Hub API ke appsettings.json.");
        }
    }
}
