using System.Text.Json;
using System.Text.Json.Nodes;

namespace DataMaster.Web.Services;

// Menulis konfigurasi Hub API (HubApiUrl/HubApiToken) - dipakai Setup Awal &
// menu Pengaturan supaya bisa diisi TANPA staf IT perlu buka file manual
// (lihat diskusi "user dipermudah, jangan disuruh edit file sendiri").
// IHostApplicationLifetime dipanggil TERPISAH oleh caller setelah menulis
// (bukan di sini) - supaya proses server di-restart Launcher & config baru
// kebaca ulang sejak proses fresh (IOptions<AppOptions> singleton, TIDAK
// hot-reload seperti IOptionsMonitor).
//
// FIX BUG NYATA 2026-09-11: SEBELUMNYA ditulis ke appsettings.json DI DALAM
// folder instalasi (env.ContentRootPath) - file itu ada di source control &
// dibundle ulang tiap rilis (nilai bawaan kosong), jadi auto-update (xcopy
// menimpa SEMUA file) diam2 ME-RESET token ini balik ke kosong tiap kali
// update terpasang, TANPA peringatan apa pun - sinkronisasi berhenti diam2.
// Sekarang ditulis ke file TERPISAH DI LUAR folder instalasi (DataDirectory
// milik Launcher, sama seperti lokasi database - lihat ServerProcessManager.cs
// yang membaca file ini & meneruskannya sbg environment variable override,
// MENANG di atas appsettings.json bawaan apa pun yang datang dari rilis baru).
public class AppSettingsWriterService(ILogger<AppSettingsWriterService> logger)
{
    private static string PathHubApiEksternal => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DataMaster", "hubapi.json");

    public async Task SetHubApiConfigAsync(string? hubApiUrl, string? hubApiToken)
    {
        try
        {
            var root = await BacaAsync();
            if (hubApiUrl is not null) root["HubApiUrl"] = hubApiUrl.Trim();
            if (hubApiToken is not null) root["HubApiToken"] = hubApiToken.Trim();
            await TulisAsync(root);
        }
        catch (Exception ex)
        {
            // Non-fatal SENGAJA - kalau gagal tulis (mis. file readonly/terkunci),
            // akun admin tetap berhasil dibuat, cuma sinkron Hub API belum aktif -
            // bisa dicoba lagi manual lewat menu Pengaturan, jangan sampai gagal
            // tulis config ikut menggagalkan Setup Awal sepenuhnya.
            logger.LogWarning(ex, "Gagal menulis konfigurasi Hub API ke file eksternal.");
        }
    }

    // Kata sandi enkripsi backup awan (2026-09-12, BUG NYATA - lihat catatan
    // panjang di UserSettingsViewModel.BackupPassphraseAktif). Ditulis ke file
    // EKSTERNAL yang SAMA (bukan file terpisah baru) - pola persis Hub API,
    // aman dari auto-update & dibaca Program.cs dgn cara yang sama persis.
    public async Task SetBackupPassphraseAsync(string passphrase)
    {
        var root = await BacaAsync();
        root["BackupPassphrase"] = passphrase.Trim();
        await TulisAsync(root);
    }

    private async Task<JsonObject> BacaAsync()
    {
        var path = PathHubApiEksternal;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return File.Exists(path)
            ? JsonNode.Parse(await File.ReadAllTextAsync(path))?.AsObject() ?? new JsonObject()
            : new JsonObject();
    }

    private static async Task TulisAsync(JsonObject root)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(PathHubApiEksternal, root.ToJsonString(options));
    }
}
