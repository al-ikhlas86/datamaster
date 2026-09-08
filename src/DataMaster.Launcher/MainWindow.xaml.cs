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
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await InisialisasiAsync();
    }

    private async Task InisialisasiAsync()
    {
        SetSplash("Memeriksa pembaruan...");
        _ = CekPembaruanLatarBelakangAsync(); // tidak memblokir start server

        SetSplash("Menyiapkan server lokal...");
        var envDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DataMaster", "webview2-data");
        Directory.CreateDirectory(envDataDir);
        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: envDataDir);
        await Browser.EnsureCoreWebView2Async(env);

        var ok = await _server.StartAsync(CancellationToken.None);
        if (!ok)
        {
            SetSplash("Gagal menyalakan server lokal. Cek log di %LocalAppData%\\DataMaster\\logs.");
            MessageBox.Show(this, "Server lokal Data Master gagal dinyalakan dalam waktu 30 detik. Periksa berkas log di %LocalAppData%\\DataMaster\\logs untuk rinciannya, lalu coba jalankan ulang aplikasi.",
                "Data Master - Gagal Start", MessageBoxButton.OK, MessageBoxImage.Error);
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

    private async Task CekPembaruanLatarBelakangAsync()
    {
        var info = await _updateChecker.CekAsync(CancellationToken.None);
        if (info.AdaPembaruan)
        {
            Dispatcher.Invoke(() =>
            {
                // Notifikasi ringan, TIDAK memblokir - unduh+pasang otomatis belum
                // diimplementasikan (lihat catatan di UpdateChecker.cs). Klik buka
                // halaman rilis GitHub di browser default utk unduh manual.
                var hasil = MessageBox.Show(this,
                    $"Versi baru Data Master tersedia ({info.VersiTerbaru}, versi terpasang saat ini {info.VersiTerpasang}).\n\nBuka halaman unduhan sekarang?",
                    "Pembaruan Tersedia", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (hasil == MessageBoxResult.Yes && info.UrlRilis is not null)
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(info.UrlRilis) { UseShellExecute = true }); }
                    catch { /* biarkan - bukan operasi kritis */ }
                }
            });
        }
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
