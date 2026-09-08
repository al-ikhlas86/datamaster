using Microsoft.Extensions.Options;

namespace DataMaster.Web.Services;

// Pengganti Task Scheduler "WebArsipData BackupCloud" (cek tiap 30 menit, jalankan
// `php spark backup:cloud` sbg proses CLI terpisah) - lihat 04-infra-auth-sync.md
// §8.2/§12. Jam ACTUAL backup dibuat tetap diatur Admin di halaman Setting (bukan
// dipaku di scheduler) - method `BuatBackupHariIniJikaPerluAsync` sendiri yang
// memutuskan apakah sudah waktunya & belum pernah dibuat hari ini (idempotent).
// `KirimAntrianAsync` jalan TIAP TICK tanpa syarat jam - berkas yang mengendap
// krn internet mati harus bisa menyusul kapan pun.
public class BackupCloudHostedService(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory, IOptions<AppOptions> options, ILogger<BackupCloudHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await JalankanSiklusAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Backup awan: siklus gagal tak terduga - lanjut ke siklus berikutnya.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task JalankanSiklusAsync(CancellationToken ct)
    {
        var url = (options.Value.HubApiUrl ?? "").Trim().TrimEnd('/');
        var token = (options.Value.HubApiToken ?? "").Trim();
        var passphrase = options.Value.BackupPassphrase ?? "";

        if (url == "" || token == "")
        {
            logger.LogInformation("Backup awan dilewati: hub_api.url/token belum diisi.");
            return;
        }
        // SENGAJA menolak jalan (bukan generate kunci acak sendiri) - kunci
        // auto-generate yang hanya ada di 1 PC berisiko membuat backup permanen
        // tidak bisa dibuka kalau PC itu mati (§8.2).
        if (passphrase.Length < 16)
        {
            logger.LogWarning("Backup awan dilewati: backup.passphrase belum diisi atau kurang dari 16 karakter. Saran acak: {Saran}", Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant());
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var backup = scope.ServiceProvider.GetRequiredService<DatabaseBackupService>();

        var dibuat = await backup.BuatBackupHariIniJikaPerluAsync(passphrase);
        if (dibuat) logger.LogInformation("Backup awan: berkas terenkripsi baru dibuat dan dimasukkan ke antrian.");

        using var http = httpClientFactory.CreateClient();
        var terkirim = await backup.KirimAntrianAsync(http, url, token);
        if (terkirim > 0) logger.LogInformation("Backup awan: {Terkirim} berkas terkirim ke Hub API.", terkirim);
    }
}
