using System.Windows;

namespace DataMaster.Launcher;

/// <summary>
/// Splash kecil berdiri sendiri (BUKAN bagian MainWindow) - dipakai HANYA
/// selama App.xaml.cs mengecek update di awal SEKALI, SEBELUM wizard/MainWindow
/// pernah ada. Tanpa ini, unduhan besar (~150MB) yang kebetulan terjadi pas
/// instalasi pertama (belum pernah lewat wizard) akan berjalan diam-diam tanpa
/// tanda visual apa pun - pelajaran nyata yang sama dgn splash MainWindow.
/// </summary>
public partial class UpdateSplashWindow : Window
{
    public UpdateSplashWindow()
    {
        InitializeComponent();
    }

    public void SetStatus(string status) => Dispatcher.Invoke(() => TxtStatus.Text = status);

    // SetProgress (2026-09-24) - null = indeterminate (belum tahu ukuran/
    // tahap ekstrak-salin), 0-100 = persentase unduhan real - lihat
    // UpdateChecker.ProgressChanged.
    public void SetProgress(double? persen) => Dispatcher.Invoke(() =>
    {
        Progress.IsIndeterminate = persen is null;
        if (persen is not null) Progress.Value = persen.Value;
    });
}
