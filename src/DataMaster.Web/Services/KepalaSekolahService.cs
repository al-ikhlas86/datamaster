using DataMaster.Data;
using DataMaster.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Services;

public record KepalaSekolahSaatIni(int GuruId, string Nama);

// Port 1:1 dari app/Models/KepalaSekolahModel.php - lihat 02-guru-kelas-struktur.md
// §6.5. "Maks 1 baris PER TAHUN AJARAN" dijaga di service layer (Tetapkan selalu
// hapus baris lama tahun itu dulu), BUKAN wipe seluruh tabel (itu perilaku LAMA
// sebelum migrasi per-tahun-ajaran, TIDAK BOLEH direplikasi).
public class KepalaSekolahService(DataMasterDbContext db)
{
    public async Task<int> ActiveTahunAjaranIdAsync()
    {
        var ta = await db.TahunAjaran.FirstOrDefaultAsync(t => t.IsActive);
        if (ta is null) throw new InvalidOperationException("Tidak ada tahun ajaran aktif - set dulu lewat menu Tahun Ajaran.");
        return ta.TahunAjaranId;
    }

    // CATATAN GAP (sama seperti PHP asli, disengaja dipertahankan apa adanya): query
    // ini TIDAK memfilter guru.status_aktif - kalau kepala sekolah dinonaktifkan lewat
    // menu Guru, method ini TETAP mengembalikannya sampai admin klik "Kosongkan" manual.
    public async Task<KepalaSekolahSaatIni?> GetSaatIniAsync(int? tahunAjaranId = null)
    {
        var ta = tahunAjaranId ?? await ActiveTahunAjaranIdAsync();
        return await db.KepalaSekolah.Where(k => k.TahunAjaranId == ta)
            .Select(k => new KepalaSekolahSaatIni(k.GuruId, k.Guru.Nama))
            .FirstOrDefaultAsync();
    }

    public async Task TetapkanAsync(int guruId, int? tahunAjaranId = null)
    {
        var ta = tahunAjaranId ?? await ActiveTahunAjaranIdAsync();
        await db.KepalaSekolah.Where(k => k.TahunAjaranId == ta).ExecuteDeleteAsync();
        db.KepalaSekolah.Add(new KepalaSekolah { GuruId = guruId, TahunAjaranId = ta });
        await db.SaveChangesAsync();
    }

    public async Task KosongkanAsync(int? tahunAjaranId = null)
    {
        var ta = tahunAjaranId ?? await ActiveTahunAjaranIdAsync();
        await db.KepalaSekolah.Where(k => k.TahunAjaranId == ta).ExecuteDeleteAsync();
    }
}
