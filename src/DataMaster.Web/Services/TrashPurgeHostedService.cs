namespace DataMaster.Web.Services;

// Cek Tempat Sampah 30 hari - pola SAMA PERSIS HubApiSyncHostedService (loop
// tunggal PeriodicTimer di dalam proses yang sama, bukan Windows Task
// Scheduler terpisah). Interval 1 jam cukup (bukan 1 menit spt sync Hub API) -
// ini cuma soal hari, bukan detik, jadi tidak perlu granularitas tinggi.
public class TrashPurgeHostedService(IServiceScopeFactory scopeFactory, ILogger<TrashPurgeHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var purge = scope.ServiceProvider.GetRequiredService<TrashPurgeService>();
                await purge.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Tempat Sampah: siklus pemusnahan gagal tak terduga - lanjut ke siklus berikutnya.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
