using System.IO;
using System.Text.Json;

namespace DataMaster.Launcher;

// Konfigurasi per-PC - file JSON terpisah di sebelah .exe, tiap PC bisa
// menimpa nilai bawaan di bawah tanpa build ulang kalau perlu.
public sealed class LauncherConfig
{
    // Token GitHub SENGAJA ditanam langsung di kode (beda dari rencana awal
    // "appsettings.json saja") - keputusan sadar 2026-09-10 setelah diskusi
    // ulang: end-user (staf TU sekolah) TIDAK mengerti cara bikin Personal
    // Access Token sendiri ("saya kira tinggal keygen"), jadi 1 token dibuat
    // SEKALI oleh pemilik proyek lalu ditanam di sini - PC manapun yang instal
    // aplikasi ini otomatis dapat auto-update aktif SEJAK AWAL, tanpa perlu
    // mengisi apapun sama sekali. Risiko diterima secara sadar: scope token
    // ini CUMA "Contents: Read-only" pada 1 repo privat ini (datamaster-desktop)
    // - kalau file .exe berhasil dibongkar & token diekstrak, dampaknya
    // terbatas (baca kode sumber, TIDAK BISA ubah apapun) dan bisa dicabut
    // kapan saja dari github.com/settings/tokens tanpa mematikan aplikasi yang
    // sudah berjalan (cuma auto-update-nya berhenti sampai token baru terbit).
    private const string EmbeddedGithubToken = "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";

    // Fine-grained PAT GitHub, scope "Contents: Read-only" KHUSUS repo
    // "datamaster-desktop" - dipakai UpdateChecker cek/unduh rilis lewat REST API
    // krn repo ini PRIVAT (URL publik "releases/latest/download/..." SELALU 404
    // tanpa kredensial utk repo privat - dibuktikan Presensi 2026-09-01, bukan
    // asumsi). Property MENTAH ini biasanya kosong (null) - PC mana pun boleh
    // mengisinya manual di appsettings.json kalau suatu saat mau pakai token
    // sendiri (mis. token bawaan dicabut) - lihat EffectiveGithubToken di bawah
    // untuk nilai yang SUNGGUHAN dipakai (fallback ke token tertanam).
    public string? GithubToken { get; set; }

    // Ini yang WAJIB dipakai UpdateChecker (bukan GithubToken mentah di atas) -
    // pakai token milik PC ini kalau sudah diisi manual, atau token bawaan
    // tertanam kalau belum. Hasilnya: auto-update SELALU aktif tanpa syarat.
    public string EffectiveGithubToken => string.IsNullOrWhiteSpace(GithubToken) ? EmbeddedGithubToken : GithubToken;

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
