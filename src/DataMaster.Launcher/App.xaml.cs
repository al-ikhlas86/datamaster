using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace DataMaster.Launcher;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    // Exception tak tertangani di WPF (termasuk dari async void event handler,
    // mis. MainWindow.Server_ExitedUnexpectedly) TIDAK OTOMATIS terlihat di mana
    // pun - default-nya aplikasi langsung mati tanpa jejak. Dicatat ke berkas log
    // yang sama dgn log server child supaya diagnosis kegagalan (mis. gagal
    // restart setelah restore) tidak butuh debugger terpasang.
    // Mutex satu-instance: dipegang selama proses hidup (sengaja disimpan di field statis supaya tidak dibuang oleh GC).
    private static Mutex? _kunciSatuInstance;

    // Hanya SATU Launcher per folder instalasi. Tanpa ini (kejadian 2026-10-29) dua Launcher berjalan bersamaan, masing-masing mengunduh
    // update lalu saling mengunci berkas, sehingga penyalinan gagal dan siklus update berputar tanpa henti. Instance yang baru dibuka ulang
    // oleh proses lama (restart setelah update / ganti jaringan) menunggu sebentar sampai proses lama selesai keluar.
    private static bool AmbilKunciSatuInstance()
    {
        var folder = AppContext.BaseDirectory.TrimEnd('\\', '/').ToLowerInvariant();
        var nama = "DataMaster.Launcher." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(folder)))[..16];
        var mutex = new Mutex(false, @"Local\" + nama);
        bool dapat;
        try { dapat = mutex.WaitOne(TimeSpan.FromSeconds(8)); }
        catch (AbandonedMutexException) { dapat = true; } // proses sebelumnya mati mendadak: kunci tetap sah diambil alih
        if (!dapat)
        {
            mutex.Dispose();
            return false;
        }
        _kunciSatuInstance = mutex;
        return true;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!AmbilKunciSatuInstance())
        {
            MessageBox.Show("Data Master sudah terbuka atau sedang memperbarui diri.\n\nTunggu sampai selesai (jangan buka lagi), atau cek jendela Data Master yang sudah ada di taskbar.",
                "Data Master", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        // Dibuka ulang oleh helper karena penyalinan update gagal: jangan langsung mengunduh lagi di sesi ini.
        if (e.Args.Contains("--update-gagal")) UpdateChecker.LewatiSesiIni = true;

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
        updateChecker.ProgressChanged += splash.SetProgress;
        var updateApplied = await updateChecker.CheckAndApplyAsync(new ServerProcessManager(), CancellationToken.None);
        if (updateApplied) return; // Shutdown() sudah dipanggil di dalam - jangan lanjut apapun lagi
        splash.Close();

        // Update otomatis ditahan karena gagal berulang, atau baru saja gagal disalin: beri tahu sekali, aplikasi lama tetap jalan.
        if (UpdateChecker.PesanTahan is { } pesanTahan)
            MessageBox.Show(pesanTahan, "Data Master - Pembaruan", MessageBoxButton.OK, MessageBoxImage.Warning);
        else if (UpdateChecker.LewatiSesiIni)
            MessageBox.Show("Pembaruan otomatis gagal menimpa berkas (kemungkinan masih terkunci oleh program lain). Data Master versi sekarang tetap bisa dipakai.\n\nTutup semua jendela Data Master lalu buka lagi untuk mencoba kembali.\nRincian: %LocalAppData%\\DataMaster\\logs\\update_helper.log",
                "Data Master - Pembaruan", MessageBoxButton.OK, MessageBoxImage.Warning);

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
