using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using WinForms = System.Windows.Forms;
// Alias eksplisit (2026-09-12) - sejak UseWindowsForms=true, beberapa nama
// tipe umum WPF jadi ambigu dgn versi System.Windows.Forms.
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace DataMaster.Launcher;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ServerProcessManager _server = new();
    private readonly UpdateChecker _updateChecker = new();
    private bool _closingIntentionally;
    private WinForms.NotifyIcon? _trayIcon;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        _server.ServerExitedUnexpectedly += Server_ExitedUnexpectedly;
        _updateChecker.StatusChanged += UpdateChecker_StatusChanged;
    }

    // BUG NYATA (2026-09-12, dilaporkan LANGSUNG user saat instalasi produksi PC
    // TK+laptop klien): tombol X SEBELUM INI SELALU mematikan server sepenuhnya
    // di mode Mandiri/Server - begitu staf TU menutup jendela ini (mengira cuma
    // "menyembunyikan", pola wajar aplikasi desktop pada umumnya), SEMUA PC
    // klien yang tersambung ikut macet ("loading terus") krn server yang
    // mereka tuju benar2 mati, bukan cuma jendelanya hilang. Padahal harapan
    // user eksplisit: "server cukup nyala & terhubung sinyal, gaperlu harus
    // buka aplikasinya" - benar, itu memang seharusnya cara kerja server yang
    // wajar, bukan permintaan aneh.
    //
    // Diperbaiki dgn pola standar aplikasi Windows sejenis (mis. Discord,
    // aplikasi antivirus, dsb) - KHUSUS mode Mandiri/Server (yang benar2
    // menjalankan server sendiri, beda dari Klien yang cuma jendela penampil):
    // tombol X TIDAK mematikan aplikasi, cuma menyembunyikan jendela ke
    // system tray (ikon kecil dekat jam). Server tetap hidup di background
    // selama proses Launcher belum benar2 di-exit. Ikon tray py menu klik-
    // kanan "Buka Data Master" (tampilkan lagi jendelanya) dan "Keluar
    // Sepenuhnya - Server Akan Berhenti" (baru benar2 mematikan, dgn
    // peringatan eksplisit dampaknya ke PC klien lain). Mode Klien TETAP
    // pakai perilaku X = keluar biasa (menutupnya cuma memutus PC itu
    // sendiri, tidak ada PC lain yang terdampak, jadi tidak perlu tray).
    private void SiapkanTrayIcon()
    {
        if (_server.IsKlien) return; // Klien: X = keluar biasa, tidak perlu tray sama sekali.

        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            var icon = exePath is not null ? System.Drawing.Icon.ExtractAssociatedIcon(exePath) : null;

            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add("Buka Data Master", null, (_, _) => TampilkanDariTray());
            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add("Keluar Sepenuhnya (server akan berhenti)", null, (_, _) => KeluarSepenuhnyaDariTray());

            _trayIcon = new WinForms.NotifyIcon
            {
                Icon = icon,
                Visible = false,
                Text = "Data Master - server sedang berjalan",
                ContextMenuStrip = menu,
            };
            _trayIcon.DoubleClick += (_, _) => TampilkanDariTray();
        }
        catch
        {
            // Non-fatal - kalau ikon tray gagal disiapkan (mis. ekstraksi ikon
            // gagal), fallback ke perilaku lama (X = keluar biasa) drpd
            // membuat aplikasi tidak bisa dipakai sama sekali.
            _trayIcon = null;
        }
    }

    private void TampilkanDariTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        if (_trayIcon is not null) _trayIcon.Visible = false;
    }

    private void KeluarSepenuhnyaDariTray()
    {
        _closingIntentionally = true;
        _trayIcon?.Dispose();
        _server.StopIntentionally();
        Application.Current.Shutdown();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await InisialisasiAsync();
    }

    private async Task InisialisasiAsync()
    {
        SiapkanTrayIcon();
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
        // Mode Mandiri/Server: tombol X TIDAK mematikan server - lihat catatan
        // panjang di SiapkanTrayIcon() kenapa ini penting (bug nyata dilaporkan
        // user, PC klien lain macet begitu jendela ini ditutup). Minimize ke
        // tray SELAMA belum benar2 diminta keluar lewat menu tray
        // (_closingIntentionally baru true dari KeluarSepenuhnyaDariTray()).
        if (_trayIcon is not null && !_closingIntentionally)
        {
            e.Cancel = true;
            Hide();
            _trayIcon.Visible = true;
            _trayIcon.ShowBalloonTip(3000, "Data Master masih berjalan",
                "Server tetap aktif di latar belakang supaya PC lain tetap bisa menyambung. Klik kanan ikon ini utk membuka lagi atau keluar sepenuhnya.",
                WinForms.ToolTipIcon.Info);
            return;
        }

        _closingIntentionally = true;
        _trayIcon?.Dispose();
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
        Process.Start(Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName!);
        Application.Current.Shutdown();
    }
}
