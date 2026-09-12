using System.IO;
using System.Text.Json;

namespace DataMaster.Launcher;

// Konfigurasi per-PC - file JSON terpisah di sebelah .exe, tiap PC bisa
// menimpa nilai bawaan di bawah tanpa build ulang kalau perlu.
public sealed class LauncherConfig
{
    // Token GitHub TIDAK ADA LAGI (2026-09-12) - repo "datamaster-desktop"
    // diubah jadi PUBLIC supaya CI/CD tidak lagi kena limit storage GitHub
    // Actions (Artifacts/Cache SELALU gratis tak terbatas utk repo public,
    // beda dari repo privat yang punya kuota kecil). Efek sampingnya:
    // UpdateChecker.cs sekarang cek/unduh rilis TANPA otentikasi apa pun -
    // endpoint GitHub REST API repo public bisa diakses siapa saja, sama
    // seperti pola Presensi. Properti token yang DULU ada di sini (termasuk
    // 1 PAT asli yang sempat tertanam di kode) sudah dihapus total, bukan
    // dipindah kemana pun - PAT lama itu WAJIB dicabut manual di
    // github.com/settings/tokens SEBELUM repo ini benar2 di-flip ke public
    // (riwayat commit lama yang masih memuat nilai token itu ikut kebuka).
    //
    // Mode multi-PC 1 jaringan lokal (LAN) - dipakai skenario "3 PC TU SD, 1
    // database utama dipakai bareng" supaya data 100% sama di semua PC (bukan
    // sinkron berkala, tapi LITERAL 1 server/1 database yang sama, PC lain
    // cuma jendela yang menampilkannya) - lihat PANDUAN-INSTALASI.md §5.
    //   "mandiri" (default)  - PC ini berdiri sendiri, server+database sendiri
    //                          di PC ini saja (perilaku SEBELUM fitur ini ada).
    //   "server"             - PC ini yang menyimpan database SUNGGUHAN, server
    //                          dengarkan SEMUA alamat jaringan (bukan cuma
    //                          127.0.0.1) di port TETAP (ServerPort) supaya PC
    //                          "klien" bisa menemukannya balik setelah restart.
    //   "klien"              - PC ini TIDAK punya database/server sendiri sama
    //                          sekali - jendela aplikasi langsung menampilkan
    //                          PC "server" lewat KlienServerUrl.
    public string Mode { get; set; } = "mandiri";

    // Wajib diisi kalau Mode="klien" - alamat PC "server" di jaringan lokal,
    // mis. "http://192.168.1.10:5250". IP PC server sebaiknya DIRESERVASI di
    // router (DHCP reservation) supaya alamat ini tidak berubah-ubah.
    public string? KlienServerUrl { get; set; }

    // Dipakai kalau Mode="server" - port TETAP (bukan port acak spt mode
    // mandiri) supaya PC klien selalu tahu ke port mana harus menyambung.
    // Firewall Windows PC server WAJIB diizinkan masuk (inbound) ke port ini -
    // langkah manual, lihat PANDUAN-INSTALASI.md §5.
    public int ServerPort { get; set; } = 5250;

    // false = wizard SetupWizardWindow WAJIB ditampilkan sebelum MainWindow
    // (lihat App.xaml.cs) - dipakai supaya Mode/dst bisa dipilih lewat layar
    // biasa saat instalasi pertama, TANPA staf IT perlu buka file appsettings.json
    // manual (keluhan nyata: "kok harus edit file sendiri, kirain tinggal pilih").
    // Di-set true otomatis begitu wizard pertama kali diselesaikan - TIDAK
    // pernah muncul lagi sesudahnya kecuali file config ini dihapus manual.
    public bool SetupSelesai { get; set; }

    private static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    public static LauncherConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                var fresh = new LauncherConfig();
                Save(fresh);
                return fresh;
            }

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<LauncherConfig>(json) ?? new LauncherConfig();
        }
        catch
        {
            return new LauncherConfig();
        }
    }

    // Publik (beda dari private Save() di bawah) - dipanggil SetupWizardWindow
    // setelah pengguna memilih Mode dst, supaya pilihannya langsung tersimpan
    // tanpa perlu tahu detail internal lokasi/format file config ini.
    public void SaveKe() => Save(this);

    private static void Save(LauncherConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { /* non-fatal - cek update murni fitur pendukung */ }
    }
}
