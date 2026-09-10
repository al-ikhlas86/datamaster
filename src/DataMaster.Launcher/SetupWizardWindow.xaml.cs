using System.Windows;

namespace DataMaster.Launcher;

/// <summary>
/// Ditampilkan SEKALI saja sebelum MainWindow, hanya kalau LauncherConfig.SetupSelesai
/// masih false (lihat App.xaml.cs) - supaya Mode (mandiri/server/klien) bisa dipilih
/// lewat layar biasa, bukan edit appsettings.json manual.
/// </summary>
public partial class SetupWizardWindow : Window
{
    private readonly LauncherConfig _config;

    public SetupWizardWindow(LauncherConfig config)
    {
        InitializeComponent();
        _config = config;
        // Nama komputer (BUKAN alamat IP) - ditampilkan di panel "Server" supaya
        // yang setting PC klien nanti tinggal baca nama ini dari layar, tidak
        // perlu ipconfig, dan tidak akan basi walau IP-nya berubah tiap restart
        // (nama Windows tidak pernah berubah sendiri, beda dari IP hasil DHCP).
        TxtNamaPcServer.Text = Environment.MachineName;
        // TxtKlienUrl SENGAJA tetap contoh generik ("NAMA-PC-SERVER", lihat XAML) -
        // BUKAN diisi Environment.MachineName PC ini sendiri, karena PC yang sedang
        // disetup sebagai klien BUKAN PC yang namanya perlu diisi di sini (yang
        // perlu diisi adalah nama PC LAIN yang berperan sebagai server).

        // Prefill dari config EXISTING - jendela ini dipakai DUA kali: (1) wizard
        // pertama kali (config masih default, prefill ini no-op krn semua masih
        // nilai bawaan), (2) dibuka ULANG lewat tombol "Ganti Pengaturan Jaringan"
        // di MainWindow (lihat MainWindow.xaml.cs) - kasus ke-2 INI yang butuh
        // prefill, supaya PC yang tadinya Klien-ke-server-A tidak harus mengetik
        // ulang semuanya dari nol cuma buat pindah ke server-B, atau parah lagi
        // kelihatan "ke-reset ke Mandiri" padahal sebenarnya masih Klien.
        TxtServerPort.Text = config.ServerPort.ToString();
        if (config.Mode == "server") RbServer.IsChecked = true;
        else if (config.Mode == "klien")
        {
            RbKlien.IsChecked = true;
            if (!string.IsNullOrWhiteSpace(config.KlienServerUrl)) TxtKlienUrl.Text = config.KlienServerUrl;
        }
    }

    private void ModePilihan_Changed(object sender, RoutedEventArgs e)
    {
        // Guard null - event ini bisa terpanggil sebelum InitializeComponent()
        // selesai menghubungkan semua elemen bernama (RbMandiri IsChecked=True
        // di XAML memicu Checked SAAT parsing, sebelum PanelServer dkk ada).
        if (PanelServer is null || PanelKlien is null || BorderDetail is null) return;

        PanelServer.Visibility = RbServer.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelKlien.Visibility = RbKlien.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        BorderDetail.Visibility = (RbServer.IsChecked == true || RbKlien.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        TxtError.Visibility = Visibility.Collapsed;
    }

    private void BtnLanjut_Click(object sender, RoutedEventArgs e)
    {
        if (RbServer.IsChecked == true)
        {
            if (!int.TryParse(TxtServerPort.Text.Trim(), out var port) || port is < 1 or > 65535)
            {
                TampilkanError("Port harus angka 1-65535.");
                return;
            }
            _config.Mode = "server";
            _config.ServerPort = port;
            _config.KlienServerUrl = null;
        }
        else if (RbKlien.IsChecked == true)
        {
            var url = TxtKlienUrl.Text.Trim();
            if (url == "" || !(url.StartsWith("http://") || url.StartsWith("https://")))
            {
                TampilkanError("Alamat PC server harus diawali http:// atau https://, contoh: http://NAMA-PC-SERVER:5250");
                return;
            }
            _config.Mode = "klien";
            _config.KlienServerUrl = url.TrimEnd('/');
        }
        else
        {
            _config.Mode = "mandiri";
            _config.KlienServerUrl = null;
        }

        _config.SetupSelesai = true;
        _config.SaveKe();

        DialogResult = true;
        Close();
    }

    private void TampilkanError(string pesan)
    {
        TxtError.Text = pesan;
        TxtError.Visibility = Visibility.Visible;
    }

    // Nama PC (TextBlock, bukan TextBox) sengaja tidak bisa di-drag-select biasa -
    // tombol ini jalan pintasnya, langsung salin bentuk SIAP TEMPEL persis format
    // yang diminta kotak "Alamat PC server" di PC klien (lihat placeholder
    // TxtKlienUrl di XAML), bukan cuma nama polos - supaya staf TU tidak perlu
    // mengetik ulang "http://" dan port-nya sendiri, sumber salah ketik yang nyata.
    private void BtnSalinAlamat_Click(object sender, RoutedEventArgs e)
    {
        var port = TxtServerPort.Text.Trim();
        if (port == "") port = "5250";
        Clipboard.SetText($"http://{TxtNamaPcServer.Text}:{port}");
        TxtSalinSukses.Visibility = Visibility.Visible;
    }
}
