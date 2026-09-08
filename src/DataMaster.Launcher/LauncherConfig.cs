using System.IO;
using System.Text.Json;

namespace DataMaster.Launcher;

// Konfigurasi per-PC - file JSON terpisah di sebelah .exe (BUKAN ditanam di kode
// terkompilasi) supaya token tidak pernah ikut ke repo/hasil build publik, dan
// tiap PC bisa diatur sendiri tanpa build ulang. Pola identik AppConfig.cs milik
// Presensi (D:\Presensi\src\Presensi\Services\AppConfig.cs) - proyek saudara yang
// sudah terbukti jalan di lapangan dgn pola yang SAMA PERSIS (repo privat + token
// fine-grained read-only).
public sealed class LauncherConfig
{
    // Fine-grained PAT GitHub, scope "Contents: Read-only" KHUSUS repo
    // "datamaster-desktop" - dipakai UpdateChecker cek/unduh rilis lewat REST API
    // krn repo ini PRIVAT (URL publik "releases/latest/download/..." SELALU 404
    // tanpa kredensial utk repo privat - dibuktikan Presensi 2026-09-01, bukan
    // asumsi). Kosong = cek pembaruan dilewati diam-diam, aplikasi tetap jalan normal.
    public string? GithubToken { get; set; }

    private static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    public static LauncherConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                var fresh = new LauncherConfig();
                Save(fresh);
                return fresh;
            }

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<LauncherConfig>(json) ?? new LauncherConfig();
        }
        catch
        {
            return new LauncherConfig();
        }
    }

    private static void Save(LauncherConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { /* non-fatal - cek update murni fitur pendukung */ }
    }
}
