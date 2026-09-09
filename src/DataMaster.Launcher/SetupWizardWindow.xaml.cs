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
                TampilkanError("Alamat PC server harus diawali http:// atau https://, contoh: http://192.168.1.10:5250");
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
}
