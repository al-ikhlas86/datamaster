using System.Diagnostics;
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
        _updateChecker.ProgressChanged += UpdateChecker_ProgressChanged;
        TaskbarItemInfo = new System.Windows.Shell.TaskbarItemInfo();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await InisialisasiAsync();
    }

    private async Task InisialisasiAsync()
    {
        SetSplash("Menyiapkan aplikasi...");

        SetSplash(_server.IsKlien ? "Menyambung ke server Data Master di jaringan..." : "Menyiapkan server lokal...");
        var envDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DataMaster", "webview2-data");
        Directory.CreateDirectory(envDataDir);
        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: envDataDir);
        await Browser.EnsureCoreWebView2Async(env);
        await BersihkanCacheBilaVersiBerubahAsync();
        // Pastikan EKSPLISIT (bukan mengandalkan default runtime WebView2 yang
        // terpasang di PC, yang bisa berbeda2/kena kebijakan GPO kantor) - staf
        // TU minta ID+password bisa diingat otomatis persis spt browser biasa
        // (mis. web Absen), supaya login berikutnya cuma tinggal klik "Masuk"
        // tanpa ketik ulang. Profil WebView2 sudah PERSISTEN per-PC (envDataDir
        // di atas), jadi begitu diaktifkan, tersimpan permanen lintas restart.
        Browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = true;
        Browser.CoreWebView2.Settings.IsGeneralAutofillEnabled = true;

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

        // Aplikasi sudah bisa dipakai: pemeriksaan + unduhan pembaruan berjalan di latar belakang (tidak menghalangi apa pun).
        _ = JalankanPembaruanLatarAsync();
    }

    // ---- pembaruan di latar belakang (alur sama dengan Keuangan, lihat UpdateChecker.cs) ----

    private readonly CancellationTokenSource _ctsPembaruan = new();

    // Loop: periksa tiap 6 jam (PC TU sering dibiarkan menyala seharian). Bila sudah siap dan pengguna memilih "Nanti", tanya lagi 3 jam kemudian.
    private async Task JalankanPembaruanLatarAsync()
    {
        var ct = _ctsPembaruan.Token;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), ct); // beri waktu aplikasi tampil dulu
            while (!ct.IsCancellationRequested)
            {
                var siap = await _updateChecker.PeriksaDanSiapkanAsync(ct);
                if (siap is not null && await TanyaRestartAsync(siap)) return; // diterapkan: aplikasi menutup diri
                await Task.Delay(TimeSpan.FromHours(siap is null ? 6 : 3), ct);
            }
        }
        catch (OperationCanceledException) { /* aplikasi ditutup */ }
    }

    /// <summary>Popup "mulai ulang sekarang?" setelah unduhan selesai. true = pengguna setuju dan penerapan dimulai.</summary>
    private async Task<bool> TanyaRestartAsync(string versi)
    {
        var jawab = await Dispatcher.InvokeAsync(() =>
        {
            var catatanServer = WindowsServiceHelper.IsInstalled()
                ? "\n\nPC ini adalah server Data Master: server berhenti beberapa detik, PC klien tersambung ulang sendiri."
                : "";
            return MessageBox.Show(this,
                $"Pembaruan Data Master versi {versi} sudah selesai diunduh dan siap dipasang.\n\nMulai ulang Data Master sekarang? Hanya butuh beberapa detik.{catatanServer}\n\n" +
                "Pilih Tidak (nanti) bila sedang bekerja: pembaruan dipasang otomatis saat Data Master dibuka lagi.",
                "Data Master - Pembaruan siap", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        });
        if (jawab != MessageBoxResult.Yes) return false;
        return await Dispatcher.InvokeAsync(() => UpdateChecker.TerapkanSekarang(_server));
    }

    // WebView2 menyimpan berkas statis di cache disk; dibersihkan SEKALI tiap versi aplikasi berubah agar tampilan lama tidak bertahan sesudah update.
    // Sesi login/kata sandi tersimpan tidak disentuh (hanya cache).
    private async Task BersihkanCacheBilaVersiBerubahAsync()
    {
        try
        {
            var versi = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?";
            var berkas = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DataMaster", "versi_terakhir.txt");
            var lama = File.Exists(berkas) ? File.ReadAllText(berkas).Trim() : "";
            if (lama == versi) return;
            await Browser.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.DiskCache | CoreWebView2BrowsingDataKinds.CacheStorage);
            File.WriteAllText(berkas, versi);
        }
        catch { /* membersihkan cache tidak boleh menghalangi aplikasi terbuka */ }
    }

    // Kemajuan unduhan hanya di judul jendela + bilah tugas (BUKAN overlay: WebView2 menutupi elemen WPF di atasnya, dan layar penuh menghalangi pekerjaan).
    private void UpdateChecker_StatusChanged(string? status)
    {
        Dispatcher.BeginInvoke(() =>
        {
            Title = status is null ? "Data Master" : $"Data Master - {status}";
            if (status is null) TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
        });
    }

    private void UpdateChecker_ProgressChanged(double? persen)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (persen is null) { TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Indeterminate; return; }
            TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
            TaskbarItemInfo.ProgressValue = Math.Clamp(persen.Value / 100.0, 0, 1);
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
        // Tombol X = keluar SEPENUHNYA, di SEMUA mode (dikembalikan 2026-09-14 -
        // lihat catatan panjang di WindowsServiceHelper.cs/ServerProcessManager.cs:
        // kebutuhan "server tetap nyala walau app ditutup" SEKARANG ditangani
        // Windows Service beneran di level OS, BUKAN lagi minimize-ke-tray WPF.
        // Tray icon SEBELUMNYA aktif juga di mode Mandiri (bukan cuma Server) -
        // BUG NYATA ditemukan 2026-09-14: user "tutup" app (kira2 benar2 keluar),
        // proses SEBENARNYA masih hidup tersembunyi di tray, lalu user buka lagi
        // -> instance KEDUA tabrakan rebutan file log HARI YANG SAMA
        // (IOException tak tertangani, "Kesalahan tak terduga") DAN rebutan
        // kunci SQLite (bikin loading lambat). Server mode via Windows Service
        // tidak butuh trik tray sama sekali lagi; Mandiri/Klien malah tidak
        // pernah butuh dari awal.
        _closingIntentionally = true;
        _ctsPembaruan.Cancel();
        _server.StopIntentionally();
        // WAJIB eksplisit sejak App.xaml pakai ShutdownMode="OnExplicitShutdown"
        // (lihat komentar di sana) - tanpa ini, menutup MainWindow normal (tombol
        // X) cuma menutup jendelanya, proses Launcher tetap hidup di background
        // tanpa jendela apapun, tidak pernah benar2 keluar.
        Application.Current.Shutdown();
    }

    // Dipakai kasus nyata: laptop mode Klien berpindah ruangan/jaringan (server
    // yang tadinya dituju sudah beda) - tanpa tombol ini, satu2nya cara ganti
    // KlienServerUrl/Mode adalah edit appsettings.json manual atau hapus baris
    // "SetupSelesai" (kasar, mereset kesan "instalasi baru"). Wizard yang SAMA
    // dipakai ulang di sini (sudah di-prefill dari config existing - lihat
    // SetupWizardWindow ctor) - bukan wizard baru terpisah, supaya UI/aturan
    // validasinya tidak dobel dirawat di 2 tempat.
    //
    // Restart PROSES PENUH (bukan cuma StartAsync ulang) SENGAJA dipilih drpd
    // hot-swap - ServerProcessManager & WebView2 (kalau Mandiri/Server) sudah
    // terlanjur dalam keadaan "siap pakai" dgn config LAMA; menukar Mode/URL
    // di tengah jalan tanpa restart penuh berisiko WebView2 tetap menampilkan
    // sesi lama sementara server di baliknya sudah beda, jauh lebih rawan bug
    // drpd 1 restart singkat yang jelas & bisa diuji.
    private void BtnGantiJaringan_Click(object sender, RoutedEventArgs e)
    {
        var config = LauncherConfig.Load();
        var wizard = new SetupWizardWindow(config) { Owner = this };
        var selesai = wizard.ShowDialog();
        if (selesai != true) return; // dibatalkan (tombol X) - config lama TIDAK disentuh

        _closingIntentionally = true;
        _server.StopIntentionally();
        // Sama alasannya dgn UpdateChecker.ApplyAndRestart() (lihat catatan
        // lengkap di situ & WindowsServiceHelper.StopDanTungguUntukUpdate())
        // - kalau PC ini SEBELUMNYA mode "server" dgn Windows Service
        // terpasang & config-nya baru saja diubah (mis. port), service LAMA
        // harus benar2 dimatikan dulu di sini SEBELUM proses baru start -
        // StopIntentionally() di atas SENGAJA tidak menyentuh service sama
        // sekali, TerapkanEnvironment/EnsureStarted() di proses baru nanti
        // cuma menyalakan yg Stopped, tidak pernah me-restart yg masih Running.
        if (WindowsServiceHelper.IsInstalled()) WindowsServiceHelper.StopDanTungguUntukUpdate();
        Process.Start(Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName!);
        Application.Current.Shutdown();
    }
}
