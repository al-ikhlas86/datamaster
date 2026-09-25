using DataMaster.Data;
using DataMaster.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Services;

// Satu-satunya tempat yang benar2 memindahkan Siswa.KelasId & mencatat RiwayatAkademik
// (2026-09-25, fitur Tahun Ajaran Kerja) - dipakai ULANG oleh AkademikController
// (ProsesNaikKelas/ProsesKelulusan/ProsesRandomKenaikan, jalur "langsung" saat TU kerja
// di tahun yang MEMANG aktif) MAUPUN TahunAjaranController.SetActive (jalur "terapkan
// rencana" saat tahun ajaran baru diaktifkan) - supaya cuma ADA 1 logic yang diuji,
// bukan beberapa implementasi terpisah yang bisa diam2 beda.
//
// TIDAK membuka/menutup transaksi sendiri - SENGAJA berjalan di dalam transaksi milik
// PEMANGGIL (lihat kedua pemanggil di atas, masing2 sudah BeginTransactionAsync sendiri).
public class KenaikanKelasService(DataMasterDbContext db)
{
    public async Task<int> TerapkanAsync(int tahunAjaranSumberId, IReadOnlyList<(int SiswaId, int? KelasTujuan, bool Lulus)> keputusan)
    {
        var ids = keputusan.Select(k => k.SiswaId).Distinct().ToList();
        if (ids.Count == 0) return 0;

        var siswaMap = await db.Siswa.Where(s => ids.Contains(s.SiswaId)).ToDictionaryAsync(s => s.SiswaId);
        var riwayatMap = await db.RiwayatAkademik.Where(r => ids.Contains(r.SiswaId) && r.TahunAjaranId == tahunAjaranSumberId).ToDictionaryAsync(r => r.SiswaId);

        var processed = 0;
        foreach (var k in keputusan)
        {
            if (!siswaMap.TryGetValue(k.SiswaId, out var siswa) || siswa.Status != StatusSiswa.aktif) continue; // skip diam-diam (race/sudah diproses) - persis perilaku lama

            if (k.Lulus)
            {
                if (riwayatMap.TryGetValue(k.SiswaId, out var existingLulus))
                {
                    existingLulus.Status = StatusRiwayatAkademik.lulus; // SUDAH ada riwayat TA ini (mis. sebelumnya 'naik') -> UPDATE, bukan insert baru
                }
                else
                {
                    var baru = new RiwayatAkademik { SiswaId = k.SiswaId, TahunAjaranId = tahunAjaranSumberId, KelasId = siswa.KelasId, Status = StatusRiwayatAkademik.lulus };
                    db.RiwayatAkademik.Add(baru);
                    riwayatMap[k.SiswaId] = baru;
                }
                siswa.Status = StatusSiswa.lulus;
                siswa.KelasId = null;
            }
            else
            {
                if (k.KelasTujuan is null or <= 0) continue;
                // Riwayat "naik" HANYA disimpan kalau siswa SEBELUMNYA sudah punya kelas,
                // DAN belum ada baris riwayat utk TA ini sama sekali - persis ProsesNaikKelas lama.
                if (siswa.KelasId is not null && !riwayatMap.ContainsKey(k.SiswaId))
                {
                    var baru = new RiwayatAkademik { SiswaId = k.SiswaId, TahunAjaranId = tahunAjaranSumberId, KelasId = siswa.KelasId, Status = StatusRiwayatAkademik.naik };
                    db.RiwayatAkademik.Add(baru);
                    riwayatMap[k.SiswaId] = baru;
                }
                siswa.KelasId = k.KelasTujuan;
            }
            processed++;
        }

        await db.SaveChangesAsync();
        return processed;
    }
}
