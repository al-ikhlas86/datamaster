using DataMaster.Data;
using DataMaster.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Services;

// Pemusnah Tempat Sampah 30 hari (2026-09-23) - lihat catatan lengkap di
// Entities/Siswa.cs::DeletedAt & SiswaController.cs::HapusPermanen/TempatSampah
// utk alasan lengkap kenapa "Hapus Permanen" tidak langsung menghapus fisik.
// HANYA baris yang: (1) DeletedAt sudah lewat 30 hari, DAN (2) TIDAK py
// riwayat akademik/ekskul (guard yang SAMA seperti rencana hard-delete
// langsung sebelumnya - cuma dipindah ke titik waktu ini) yang benar2
// dimusnahkan (db.Siswa.Remove). Baris yang lewat 30 hari TAPI masih py
// riwayat dibiarkan "macet" di sampah (terlihat di TempatSampah() dgn
// MacetPunyaRiwayat=true) - JANGAN PERNAH auto-hapus riwayat sungguhan
// hanya krn timer lewat.
public class TrashPurgeService(DataMasterDbContext db, DocumentStorageService docs, ILogger<TrashPurgeService> logger)
{
    private const int MasaSampahHari = 30;

    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        var batas = DateTime.UtcNow.AddDays(-MasaSampahHari);
        var kandidat = await db.Siswa
            .Include(s => s.RiwayatAkademikList)
            .Include(s => s.EkskulSiswaList)
            .Where(s => s.DeletedAt != null && s.DeletedAt <= batas)
            .ToListAsync(ct);

        var dimusnahkan = 0;
        foreach (var siswa in kandidat)
        {
            if (siswa.RiwayatAkademikList.Count > 0 || siswa.EkskulSiswaList.Count > 0)
            {
                logger.LogWarning("Tempat Sampah: '{Nama}' (ID {Id}) sudah lewat {Hari} hari tapi TIDAK dimusnahkan - masih py riwayat akademik/ekskul tersimpan.", siswa.Nama, siswa.SiswaId, MasaSampahHari);
                continue;
            }

            docs.Delete(siswa.DokumenKk);
            docs.Delete(siswa.DokumenAkta);
            docs.Delete(siswa.DokumenKia);
            docs.Delete(siswa.DokumenIjazah);

            // HARD DELETE beneran - baris hilang dari kiriman full:true
            // berikutnya, Hub API men-tombstone otomatis (lihat
            // BaseSyncModel::tombstoneMissing di hub-api), VPS ikut coba
            // hapus fisik (lihat hubApiSync.js - ditolak otomatis kalau
            // ternyata py presensi/izin asli di sana).
            db.Siswa.Remove(siswa);
            logger.LogInformation("Tempat Sampah: '{Nama}' (ID {Id}) dimusnahkan permanen setelah {Hari} hari.", siswa.Nama, siswa.SiswaId, MasaSampahHari);
            dimusnahkan++;
        }

        if (dimusnahkan > 0) await db.SaveChangesAsync(ct);
        return dimusnahkan;
    }
}
