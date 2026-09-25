using DataMaster.Data;
using DataMaster.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Services;

// Tahun Ajaran Kerja (TAK, 2026-09-25) - konteks tahun ajaran yang sedang DIKERJAKAN TU
// di halaman2 yang tahun-ajaran-scoped (Kurikulum, Jam Belajar, Jadwal Pelajaran, Wali
// Kelas, Kepala Sekolah, Kalender Akademik), TERPISAH dari tahun ajaran yang sedang
// AKTIF (TahunAjaran.IsActive - yang menentukan apa yang terlihat orang tua/guru di
// webview-app/mobile-app). Disimpan GLOBAL di SystemSettings (BUKAN Session/Cookie -
// Session di app ini timeout 30 menit, terlalu pendek utk penyusunan rencana kenaikan
// kelas yang realistisnya butuh berhari-hari; dan harus 1 konteks yang sama dilihat
// SEMUA akun TU, bukan preferensi personal per-browser).
//
// PENTING: service ini HANYA utk resolusi "tahun ajaran mana yang jadi DEFAULT tampilan
// halaman kalau TU tidak pilih eksplisit" - method WaliKelasService.ActiveTahunAjaranIdAsync()
// / KepalaSekolahService.ActiveTahunAjaranIdAsync() (dan pengecekan TahunAjaran.IsActive
// langsung di controller lain) TETAP berarti tahun aktif SESUNGGUHNYA, JANGAN diganti
// jadi TAK - itu dipakai jaga invarian LIVE (mis. sinkronisasi Guru.Jabatan di
// WaliKelasService.TetapkanSemuaAsync) yang HARUS tetap terikat tahun yang benar2
// aktif, bukan tahun yang lagi disiapkan TU.
public class TahunAjaranKerjaService(DataMasterDbContext db)
{
    private const string SettingKey = "tahun_ajaran_kerja_id";

    // Fallback ke tahun ajaran AKTIF kalau setting belum pernah diisi (instalasi lama
    // yang belum pernah pakai fitur ini) ATAU nilainya nunjuk tahun yang sudah tidak
    // ada lagi - supaya perilaku default identik hari-ini sampai TU sengaja ganti.
    public async Task<int> GetKerjaIdAsync()
    {
        var row = await db.SystemSettings.FindAsync(SettingKey);
        if (row?.SettingValue is { } v && int.TryParse(v, out var id) && await db.TahunAjaran.AnyAsync(t => t.TahunAjaranId == id))
        {
            return id;
        }
        var aktif = await db.TahunAjaran.Where(t => t.IsActive).Select(t => t.TahunAjaranId).FirstOrDefaultAsync();
        return aktif;
    }

    public async Task SetKerjaIdAsync(int tahunAjaranId)
    {
        var existing = await db.SystemSettings.FindAsync(SettingKey);
        if (existing is null) db.SystemSettings.Add(new SystemSetting { SettingKey = SettingKey, SettingValue = tahunAjaranId.ToString(), UpdatedAt = DateTime.Now });
        else { existing.SettingValue = tahunAjaranId.ToString(); existing.UpdatedAt = DateTime.Now; }
        await db.SaveChangesAsync();
    }

    // Dipanggil TahunAjaranController.SetActive - TAK ikut "pulang" ke tahun yang baru
    // diaktifkan, supaya semua TU otomatis balik ke tampilan normal (bukan nyangkut di
    // tahun yang baru saja selesai diproses).
    public Task ResetKeAktifAsync(int tahunAjaranId) => SetKerjaIdAsync(tahunAjaranId);
}
