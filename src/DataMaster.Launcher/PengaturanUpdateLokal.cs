using System.IO;
using System.Text.Json;

namespace DataMaster.Launcher;

/// <summary>
/// Pengaturan pembaruan PER PC (2026-11-27): "restart otomatis setelah pembaruan diunduh". BAWAAN MATI: pembaruan hanya diberi tahu lewat notifikasi pojok kanan
/// bawah dan dipasang saat pengguna memilih mulai ulang atau saat aplikasi dibuka lagi. Disimpan di %LocalAppData%\DataMaster\update_pengaturan.json (per pengguna
/// Windows), diubah dari halaman Pengaturan lewat jembatan pesan WebView2 (MainWindow).
/// </summary>
internal static class PengaturanUpdateLokal
{
    private sealed class Isi { public bool AutoRestart { get; set; } }

    private static string Berkas => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DataMaster", "update_pengaturan.json");

    public static bool AutoRestart
    {
        get
        {
            try { return File.Exists(Berkas) && (JsonSerializer.Deserialize<Isi>(File.ReadAllText(Berkas))?.AutoRestart ?? false); }
            catch { return false; }
        }
    }

    public static void Simpan(bool autoRestart)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Berkas)!);
            File.WriteAllText(Berkas, JsonSerializer.Serialize(new Isi { AutoRestart = autoRestart }));
        }
        catch { /* gagal menyimpan: pengaturan tetap bawaan (mati), tidak boleh mengganggu aplikasi */ }
    }
}
