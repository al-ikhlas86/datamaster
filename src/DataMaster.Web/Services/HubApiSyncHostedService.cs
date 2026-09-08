namespace DataMaster.Web.Services;

// Pengganti Task Scheduler "WebArsipData SyncPush" (cek tiap 1 menit, jalankan
// `php spark sync:push` sbg proses CLI terpisah) - lihat 04-infra-auth-sync.md §12.
// ADAPTASI SENGAJA: desktop app ini long-running SELAMA WebView2 terbuka (beda
// dari server PHP yang selalu hidup 24/7 lewat Apache) - PeriodicTimer di DALAM
// proses yang sama jauh lebih sederhana drpd menjadwalkan proses CLI terpisah lewat
// Windows Task Scheduler, dan tetap memenuhi semangat "coba tiap ~1 menit, lanjut
// otomatis kalau gagal". Loop TUNGGAL (bukan spawn per-tick) secara alami mencegah
// overlap - setara "Task Scheduler skip kalau eksekusi sebelumnya masih berjalan".
public class HubApiSyncHostedService(IServiceScopeFactory scopeFactory, ILogger<HubApiSyncHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<HubApiSyncService>();
                await sync.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sync Hub API: siklus gagal tak terduga - lanjut ke siklus berikutnya.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
