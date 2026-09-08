using System.IO;
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
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            CatatErrorFatal(args.Exception);
            MessageBox.Show($"Terjadi kesalahan tak terduga:\n\n{args.Exception.Message}\n\nRincian lengkap dicatat di %LocalAppData%\\DataMaster\\logs.",
                "Data Master - Kesalahan", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true; // jangan langsung matikan aplikasi kalau masih bisa dipulihkan
        };
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
