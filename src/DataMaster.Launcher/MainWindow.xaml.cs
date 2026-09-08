using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace DataMaster.Launcher;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ServerProcessManager _server = new();
    private readonly UpdateChecker _updateChecker = new();
    private bool _closingIntentionally;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        _server.ServerExitedUnexpectedly += Server_ExitedUnexpectedly;
        _updateChecker.StatusChanged += UpdateChecker_StatusChanged;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await InisialisasiAsync();
    }

    private async Task InisialisasiAsync()
    {
        SetSplash("Memeriksa pembaruan...");
        // Tidak memblokir start server - kalau ADA pembaruan, UpdateChecker
        // sendiri yang akan mematikan _server & menutup aplikasi lewat
        // StatusChanged/ApplyAndRestart (lihat UpdateChecker_StatusChanged).
        _ = _updateChecker.CheckAndApplyAsync(_server, CancellationToken.None);

        SetSplash(_server.IsKlien ? "Menyambung ke server Data Master di jaringan..." : "Menyiapkan server lokal...");
        var envDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DataMaster", "webview2-data");
        Directory.CreateDirectory(envDataDir);
        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: envDataDir);
        await Browser.EnsureCoreWebView2Async(env);

        var ok = await _server.StartAsync(CancellationToken.None);
        if (!ok)
        {
            if (_server.IsKlien)
            {
                SetSplash("Gagal menyambung ke server Data Master.");
                MessageBox.Show(this, "Tidak dapat menyambung ke PC server Data Master di jaringan. Pastikan PC server sudah menyala & aplikasi Data Master-nya sudah terbuka, PC ini terhubung ke jaringan yang sama, dan alamat di appsettings.json (KlienServerUrl) masih benar - lalu coba buka ulang aplikasi ini.",
                    "Data Master - Gagal Menyambung", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                SetSplash("Gagal menyalakan server lokal. Cek log di %LocalAppData%\\DataMaster\\logs.");
                MessageBox.Show(this, "Server lokal Data Master gagal dinyalakan dalam waktu 30 detik. Periksa berkas log di %LocalAppData%\\DataMaster\\logs untuk rinciannya, lalu coba jalankan ulang aplikasi.",
                    "Data Master - Gagal Start", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Close();
            return;
        }

        SetSplash("Membuka aplikasi...");
        Browser.CoreWebView2.Navigate(_server.BaseUrl);
        Browser.CoreWebView2.NavigationCompleted += (_, args) =>
        {
            if (args.IsSuccess) TampilkanBrowser();
        };
    }

    // Dipanggil UpdateChecker saat status unduh/pasang berubah - null berarti
    // "sembunyikan lagi" (gagal/dibatalkan). Splash overlay dipaksa tampil lagi
    // supaya pesan "JANGAN TUTUP APLIKASI" terlihat meski browser sudah kadung
    // tampil - pelajaran nyata dari Presensi (lihat komentar UpdateChecker.cs).
    private void UpdateChecker_StatusChanged(string? status)
    {
        Dispatcher.Invoke(() =>
        {
            if (status is null)
            {
                if (Browser.Visibility == Visibility.Visible) SplashOverlay.Visibility = Visibility.Collapsed;
                return;
            }
            Browser.Visibility = Visibility.Collapsed;
            SplashOverlay.Visibility = Visibility.Visible;
            SplashStatus.Text = status;
        });
    }

    private void Server_ExitedUnexpectedly()
    {
        // SATU2NYA penyebab sah server berhenti sendiri: restore database
        // (DatabaseBackupService.RestoreAsync -> StopApplication) - lihat komentar
        // ServerProcessManager. Restart otomatis, tampilkan splash lagi sebentar.
        Dispatcher.Invoke(async () =>
        {
            if (_closingIntentionally) return;
            Browser.Visibility = Visibility.Collapsed;
            SplashOverlay.Visibility = Visibility.Visible;
            SetSplash("Menerapkan pembaruan data, menyalakan ulang...");

            await Task.Delay(1000); // beri jeda singkat memastikan file/port benar2 lepas
            var ok = await _server.StartAsync(CancellationToken.None);
            if (!ok)
            {
                SetSplash("Gagal menyalakan ulang server. Silakan tutup dan buka ulang aplikasi.");
                return;
            }
            Browser.CoreWebView2.Navigate(_server.BaseUrl);
        });
    }

    private void TampilkanBrowser()
    {
        SplashOverlay.Visibility = Visibility.Collapsed;
        Browser.Visibility = Visibility.Visible;
    }

    private void SetSplash(string status) => Dispatcher.Invoke(() => SplashStatus.Text = status, DispatcherPriority.Background);

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _closingIntentionally = true;
        _server.StopIntentionally();
    }
}
