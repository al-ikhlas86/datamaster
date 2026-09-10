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
}
