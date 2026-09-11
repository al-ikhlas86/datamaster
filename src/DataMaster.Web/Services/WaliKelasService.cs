using DataMaster.Data;
using DataMaster.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Services;

// Port 1:1 dari app/Models/WaliKelasModel.php - model TERPENTING modul Guru/Kelas,
// menjaga SEMUA invarian relasi guru<->kelas<->tahun. Lihat 02-guru-kelas-struktur.md §11.
public class WaliKelasService(DataMasterDbContext db)
{
    public async Task<int> ActiveTahunAjaranIdAsync()
    {
        var ta = await db.TahunAjaran.FirstOrDefaultAsync(t => t.IsActive);
        if (ta is null) throw new InvalidOperationException("Tidak ada tahun ajaran aktif - set dulu lewat menu Tahun Ajaran.");
        return ta.TahunAjaranId;
    }

    // Versi TIDAK MELEMPAR (2026-09-11) - dipakai method DISPLAY-ONLY di bawah
    // (GetDisplayTextAsync dkk). Sebelumnya semuanya memakai ActiveTahunAjaranIdAsync()
    // langsung, jadi kalau instalasi BARU belum sempat set Tahun Ajaran Aktif sama
    // sekali, buka Detail Kelas/Guru manapun langsung CRASH TOTAL (halaman error
    // generik ASP.NET Core) - dilaporkan user 2026-09-11 (instalasi TKIT baru).
    // Method yang MENULIS/MENGUBAH data (assign wali kelas dkk) TETAP pakai versi
    // melempar di atas - itu benar kalau ditolak keras, cuma method BACA yang
    // seharusnya tetap tampil (tinggal kosongkan info yang butuh tahun aktif).
    private async Task<int?> ActiveTahunAjaranIdOrNullAsync() =>
        (await db.TahunAjaran.FirstOrDefaultAsync(t => t.IsActive))?.TahunAjaranId;

    public async Task<Kelas?> GetKelasByGuruAsync(int guruId, int? tahunAjaranId = null)
    {
        var ta = tahunAjaranId ?? await ActiveTahunAjaranIdOrNullAsync();
        if (ta is null) return null; // belum ada Tahun Ajaran Aktif - kosong, jangan crash (lihat GetDisplayTextAsync)
        return await db.WaliKelas.Where(w => w.GuruId == guruId && w.TahunAjaranId == ta)
            .Select(w => w.Kelas).FirstOrDefaultAsync();
    }

    public async Task<string?> GetDisplayTextAsync(int kelasId, int? tahunAjaranId = null)
    {
        var ta = tahunAjaranId ?? await ActiveTahunAjaranIdOrNullAsync();
        if (ta is null) return null; // belum ada Tahun Ajaran Aktif - tampilkan kosong, jangan crash
        var namaList = await db.WaliKelas.Where(w => w.KelasId == kelasId && w.TahunAjaranId == ta)
            .OrderBy(w => w.Urutan).Select(w => w.Guru.Nama).ToListAsync();
        if (namaList.Count == 0) return null;
        if (namaList.Count == 1) return namaList[0];
        // Titel SAJA yang beda (2026-09-11, poin #2) - slot pertama tetap "Wali
        // Kelas", sisanya "Pendamping" - lihat catatan lengkap di Kelas/Index.cshtml.
        return $"{namaList[0]} (Wali Kelas)" + string.Concat(namaList.Skip(1).Select(n => $" & {n} (Pendamping)"));
    }

    // kelas_id -> daftar {guru_id, nama} terurut - dipakai membangun combobox multi-slot
    // di halaman Kelola Kelas tanpa query N+1.
    public async Task<Dictionary<int, List<(int GuruId, string Nama)>>> GetSemuaDikelompokkanAsync(int? tahunAjaranId = null)
    {
        var ta = tahunAjaranId ?? await ActiveTahunAjaranIdOrNullAsync();
        if (ta is null) return []; // belum ada Tahun Ajaran Aktif - kosong, jangan crash (lihat GetDisplayTextAsync)
        var rows = await db.WaliKelas.Where(w => w.TahunAjaranId == ta && w.Kelas.IsActive)
            .OrderBy(w => w.Urutan)
            .Select(w => new { w.KelasId, w.GuruId, Nama = w.Guru.Nama })
            .ToListAsync();
        return rows.GroupBy(r => r.KelasId).ToDictionary(g => g.Key, g => g.Select(r => (r.GuruId, r.Nama)).ToList());
    }

    /// <summary>
    /// Tunjuk SEMUA wali 1 kelas sekaligus dari daftar guru_id (urutan array = urutan
    /// slot, jumlah TIDAK dibatasi). Baris sebelumnya ADA di tahun ini tapi tidak ikut
    /// dikirim lagi dianggap dikosongkan. Return: guru_id -> nama kelas lama yang dilepas.
    /// </summary>
    public async Task<Dictionary<int, string>> TetapkanSemuaAsync(int kelasId, List<int> guruIds, int? tahunAjaranId = null)
    {
        var ta = tahunAjaranId ?? await ActiveTahunAjaranIdAsync();
        var dipakai = guruIds.Where(id => id > 0).Distinct().ToList();

        var lama = await db.WaliKelas.Where(w => w.KelasId == kelasId && w.TahunAjaranId == ta).Select(w => w.GuruId).ToListAsync();

        var dilepasDari = new Dictionary<int, string>();
        foreach (var guruId in dipakai)
        {
            var existing = await db.WaliKelas.Where(w => w.GuruId == guruId && w.TahunAjaranId == ta && w.KelasId != kelasId)
                .Select(w => new { w.KelasId, w.Kelas.NamaKelas }).FirstOrDefaultAsync();
            if (existing is not null) dilepasDari[guruId] = existing.NamaKelas;

            // Hapus guru ini dari MANA PUN dia ada DI TAHUN INI (cegah bentrok UNIQUE
            // (guru_id,tahun_ajaran_id)) - SENGAJA dibatasi where tahun_ajaran_id supaya
            // assignment guru yang sama di tahun LAIN tidak ikut terhapus.
            await db.WaliKelas.Where(w => w.GuruId == guruId && w.TahunAjaranId == ta).ExecuteDeleteAsync();
        }

        await db.WaliKelas.Where(w => w.KelasId == kelasId && w.TahunAjaranId == ta).ExecuteDeleteAsync();

        for (var i = 0; i < dipakai.Count; i++)
        {
            db.WaliKelas.Add(new WaliKelas { KelasId = kelasId, GuruId = dipakai[i], TahunAjaranId = ta, Urutan = (byte)(i + 1) });
        }
        await db.SaveChangesAsync();

        // Sinkronisasi jabatan HANYA kalau tahun yang diedit = tahun aktif - edit
        // assignment tahun BUKAN aktif TIDAK BOLEH mengubah guru.jabatan hari ini.
        if (ta == await ActiveTahunAjaranIdAsync())
        {
            await SinkronJabatanGuruAsync(lama.Concat(dipakai).Distinct().ToList());
        }

        return dilepasDari;
    }

    private async Task SinkronJabatanGuruAsync(List<int> guruIds)
    {
        var aktifTa = await ActiveTahunAjaranIdAsync();
        foreach (var guruId in guruIds)
        {
            var guru = await db.Guru.FindAsync(guruId);
            if (guru is null || guru.Jabatan == JabatanGuru.karyawan) continue; // Karyawan SENGAJA tidak pernah disentuh.
            var masihWali = await db.WaliKelas.AnyAsync(w => w.GuruId == guruId && w.TahunAjaranId == aktifTa);
            guru.Jabatan = masihWali ? JabatanGuru.guru_kelas : JabatanGuru.guru_bidang;
        }
        await db.SaveChangesAsync();
    }
}
