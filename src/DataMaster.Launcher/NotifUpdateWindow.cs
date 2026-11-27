using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DataMaster.Launcher;

/// <summary>
/// Notifikasi pembaruan di pojok kanan bawah layar (2026-11-27). Jendela TERSENDIRI yang selalu di atas dan tidak mengambil fokus: WebView2 menutupi elemen WPF
/// yang ditaruh di atasnya, dan popup modal mengganggu orang yang sedang mengetik. Dua mode:
///  - manual (bawaan): "Pembaruan siap. Mulai ulang sekarang / Nanti".
///  - otomatis (pilihan di Pengaturan): hitung mundur 60 dtk lalu mulai ulang sendiri, bisa ditunda lewat "Tunda".
/// Salinan dari Keuangan (nama aplikasi diberikan lewat konstruktor).
/// </summary>
public sealed class NotifUpdateWindow : Window
{
    public event Action? MulaiUlang;
    public event Action? Nanti;

    private readonly TextBlock _teks = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White, FontSize = 13, MaxWidth = 320 };
    private readonly DispatcherTimer? _hitung;
    private readonly string _pesanOtomatis;
    private int _sisa = 60;
    private bool _selesai;

    public NotifUpdateWindow(string namaApp, string versi, bool otomatis, bool pcServer)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false; // jangan merebut fokus dari kolom yang sedang diketik
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37));
        Title = $"{namaApp} - pembaruan siap";

        var catatan = pcServer ? " PC ini server: server berhenti beberapa detik, PC klien tersambung ulang sendiri." : "";
        _pesanOtomatis = $"Pembaruan {namaApp} versi {versi} siap. {namaApp} mulai ulang otomatis dalam {{0}} detik.{catatan}";
        _teks.Text = otomatis
            ? string.Format(_pesanOtomatis, _sisa)
            : $"Pembaruan {namaApp} versi {versi} sudah diunduh. Mulai ulang {namaApp} untuk memasangnya (hanya beberapa detik).{catatan}";

        var tombolMulai = Tombol("Mulai ulang sekarang", Color.FromRgb(0x22, 0xC5, 0x5E), Colors.White);
        var tombolNanti = Tombol(otomatis ? "Tunda" : "Nanti", Color.FromRgb(0x37, 0x41, 0x51), Colors.White);
        tombolMulai.Click += (_, _) => Selesaikan(true);
        tombolNanti.Click += (_, _) => Selesaikan(false);

        var tombol = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        tombol.Children.Add(tombolMulai);
        tombol.Children.Add(tombolNanti);
        var isi = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };
        isi.Children.Add(_teks);
        isi.Children.Add(tombol);
        Content = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x63)), BorderThickness = new Thickness(1), Child = isi };

        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 16;
            Top = area.Bottom - ActualHeight - 16;
        };
        Closed += (_, _) => { _hitung?.Stop(); if (!_selesai) { _selesai = true; Nanti?.Invoke(); } };

        if (otomatis)
        {
            _hitung = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _hitung.Tick += (_, _) =>
            {
                _sisa--;
                if (_sisa <= 0) { Selesaikan(true); return; }
                _teks.Text = string.Format(_pesanOtomatis, _sisa);
            };
            _hitung.Start();
        }
    }

    private static Button Tombol(string teks, Color warna, Color huruf) => new()
    {
        Content = teks, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0), BorderThickness = new Thickness(0),
        Background = new SolidColorBrush(warna), Foreground = new SolidColorBrush(huruf), Cursor = System.Windows.Input.Cursors.Hand,
    };

    private void Selesaikan(bool mulai)
    {
        if (_selesai) return;
        _selesai = true;
        _hitung?.Stop();
        if (mulai) MulaiUlang?.Invoke(); else Nanti?.Invoke();
        Close();
    }
}
