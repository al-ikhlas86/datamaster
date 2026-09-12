using System.IO;
using System.Windows;
using System.Windows.Threading;
// Alias eksplisit (2026-09-12) - sejak UseWindowsForms=true (utk NotifyIcon
// system tray di MainWindow.xaml.cs), "MessageBox"/"Application" polos
// ambigu dgn versi System.Windows.Forms.
using MessageBox = System.Windows.MessageBox;

namespace DataMaster.Launcher;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
// System.Windows.Application dieksplisit qualified (2026-09-12) - sejak
// UseWindowsForms=true ditambahkan (utk NotifyIcon system tray di
// MainWindow.xaml.cs), "Application" polos jadi ambigu dgn
// System.Windows.Forms.Application yang ikut masuk implicit usings.
public partial class App : System.Windows.Application
{
    // Exception tak tertangani di WPF (termasuk dari async void event handler,
    // mis. MainWindow.Server_ExitedUnexpectedly) TIDAK OTOMATIS terlihat di mana
    // pun - default-nya aplikasi langsung mati tanpa jejak. Dicatat ke berkas log
    // yang sama dgn log server child supaya diagnosis kegagalan (mis. gagal
    // restart setelah restore) tidak butuh debugger terpasang.
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Rotasi log lama SEDINI mungkin (sebelum server sempat start) - supaya
        // tetap jalan walau server lokal gagal start (skenario itu jugalah yang
        // paling butuh log tetap bersih & terbaca, bukan tenggelam file lama).
        try
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DataMaster", "logs");
            LogCleanup.RotasiLogLama(logDir);
        }
        catch { /* non-fatal */ }

        DispatcherUnhandledException += (_, args) =>
        {
            CatatErrorFatal(args.Exception);
            MessageBox.Show($"Terjadi kesalahan tak terduga:\n\n{args.Exception.Message}\n\nRincian lengkap dicatat di %LocalAppData%\\DataMaster\\logs.",
                "Data Master - Kesalahan", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true; // jangan langsung matikan aplikasi kalau masih bisa dipulihkan
        };

        // Cek update SEDINI mungkin - SEBELUM wizard pun sempat tampil. Dulu
        // UpdateChecker cuma jalan di dalam MainWindow, yang baru ada SETELAH
        // wizard selesai diisi - artinya PC yang baru instal & belum pernah
        // menyelesaikan wizard TIDAK PERNAH ke-cek update sama sekali, staf IT
        // terpaksa unduh manual dari GitHub Releases sendiri (gap nyata,
        // ditemukan langsung saat uji instalasi bersih v1.0.5/v1.0.6). Splash
        // kecil terpisah (BUKAN splash MainWindow, krn MainWindow belum ada di
        // titik ini) supaya unduhan besar tidak berjalan diam-diam tanpa tanda.
        var splash = new UpdateSplashWindow();
        splash.Show();
        var updateChecker = new UpdateChecker();
        updateChecker.StatusChanged += status => { if (status is not null) splash.SetStatus(status); };
        var updateApplied = await updateChecker.CheckAndApplyAsync(new ServerProcessManager(), CancellationToken.None);
        if (updateApplied) return; // Shutdown() sudah dipanggil di dalam - jangan lanjut apapun lagi
        splash.Close();

        // Wizard cara-pakai (mandiri/server/klien) ditampilkan SEKALI saja sebelum
        // MainWindow pernah ada - lihat komentar App.xaml soal StartupUri yang
        // sengaja dihapus. Kalau user menutup wizard tanpa memilih (tombol X),
        // keluar bersih - jangan lanjut dgn config yang belum sempat disimpan.
        var config = LauncherConfig.Load();
        if (!config.SetupSelesai)
        {
            var wizard = new SetupWizardWindow(config);
            var selesai = wizard.ShowDialog();
            if (selesai != true)
            {
                Shutdown();
                return;
            }
        }

        var main = new MainWindow();
        MainWindow = main;
        main.Show();
    }

    private static void CatatErrorFatal(Exception ex)
    {
        try
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DataMaster", "logs");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, $"launcher_{DateTime.Now:yyyy-MM-dd}.log"), $"[{DateTime.Now:O}] {ex}\n\n");
        }
        catch { /* jangan sampai logging error justru melempar error baru */ }
    }
}
