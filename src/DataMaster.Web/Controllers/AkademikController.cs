using DataMaster.Data;
using DataMaster.Data.Entities;
using DataMaster.Web.Models.Akademik;
using DataMaster.Web.Models.Siswa;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Controllers;

// Port 1:1 dari app/Controllers/Akademik.php - lihat 03-akademik-jadwal.md §9.
// BEDA TOTAL dari Kurikulum/Jadwal/Kalender - modul ini murni proses kenaikan
// kelas & kelulusan akhir tahun ajaran, arsip historis, dan rekap lulusan.
[Route("akademik")]
public class AkademikController(DataMasterDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var tahunAktif = await db.TahunAjaran.FirstOrDefaultAsync(t => t.IsActive);
        var vm = new AkademikIndexViewModel { AdaTahunAktif = tahunAktif is not null, TahunAktifNama = tahunAktif?.Nama };
        if (tahunAktif is null) return View(vm);

        var siswaAktif = await db.Siswa.Include(s => s.Kelas).Where(s => s.Status == StatusSiswa.aktif).OrderBy(s => s.Nama).ToListAsync();
        var tingkatMaster = await db.Tingkat.ToDictionaryAsync(t => t.Kode);

        vm.KelompokSiswa = siswaAktif
            .GroupBy(s => s.KelasId)
            .Select(g => new KelompokKelasSiswa
            {
                KelasId = g.Key,
                NamaKelas = g.Key is null ? "Tanpa Kelas" : g.First().Kelas!.NamaKelas,
                Tingkat = g.Key is null ? "" : g.First().Kelas!.Tingkat,
                SiswaList = g.Select(s => new SiswaAktifRow { SiswaId = s.SiswaId, Nama = s.Nama, Nis = s.Nis, JenisKelamin = s.JenisKelamin.ToString() }).ToList(),
            })
            .OrderBy(k => k.KelasId is null ? 99 : (tingkatMaster.TryGetValue(k.Tingkat, out var t) ? t.Urutan : 98))
            .ToList();

        vm.KelasList = await db.Kelas.Where(k => k.IsActive).OrderBy(k => k.Tingkat).ThenBy(k => k.NamaKelas)
            .Select(k => new KelasOption { KelasId = k.KelasId, NamaKelas = k.NamaKelas }).ToListAsync();

        return View(vm);
    }

    [HttpPost("proses-naik-kelas")]
    public async Task<IActionResult> ProsesNaikKelas(List<int>? siswa_ids, int? kelas_tujuan)
    {
        var tahunAktif = await db.TahunAjaran.FirstOrDefaultAsync(t => t.IsActive);
        if (tahunAktif is null)
        {
            TempData["error"] = "Tidak ada tahun ajaran aktif. Silakan aktifkan terlebih dahulu.";
            return RedirectToAction(nameof(Index));
        }
        var ids = siswa_ids ?? [];
        if (ids.Count == 0)
        {
            TempData["warning"] = "Tidak ada siswa yang dipilih.";
            return RedirectToAction(nameof(Index));
        }
        if (kelas_tujuan is null or <= 0)
        {
            TempData["error"] = "Kelas tujuan wajib dipilih.";
            return RedirectToAction(nameof(Index));
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        var processed = 0;
        try
        {
            foreach (var siswaId in ids)
            {
                var siswa = await db.Siswa.FindAsync(siswaId);
                if (siswa is null || siswa.Status != StatusSiswa.aktif) continue; // skip diam-diam (race/sudah diproses)

                var sudahAda = await db.RiwayatAkademik.AnyAsync(r => r.SiswaId == siswaId && r.TahunAjaranId == tahunAktif.TahunAjaranId);
                if (!sudahAda)
                {
                    db.RiwayatAkademik.Add(new RiwayatAkademik { SiswaId = siswaId, TahunAjaranId = tahunAktif.TahunAjaranId, KelasId = siswa.KelasId, Status = StatusRiwayatAkademik.naik });
                }

                siswa.KelasId = kelas_tujuan;
                // Status TETAP 'aktif'.
                processed++;
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            TempData["error"] = "Terjadi kesalahan saat memproses kenaikan kelas.";
            return RedirectToAction(nameof(Index));
        }

        TempData["message"] = $"{processed} siswa berhasil dinaikkan kelas.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("proses-kelulusan")]
    public async Task<IActionResult> ProsesKelulusan(List<int>? siswa_ids)
    {
        var tahunAktif = await db.TahunAjaran.FirstOrDefaultAsync(t => t.IsActive);
        if (tahunAktif is null)
        {
            TempData["error"] = "Tidak ada tahun ajaran aktif. Silakan aktifkan terlebih dahulu.";
            return RedirectToAction(nameof(Index));
        }
        var ids = siswa_ids ?? [];
        if (ids.Count == 0)
        {
            TempData["warning"] = "Tidak ada siswa yang dipilih.";
            return RedirectToAction(nameof(Index));
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        var processed = 0;
        try
        {
            foreach (var siswaId in ids)
            {
                var siswa = await db.Siswa.FindAsync(siswaId);
                if (siswa is null || siswa.Status != StatusSiswa.aktif) continue;

                var existing = await db.RiwayatAkademik.FirstOrDefaultAsync(r => r.SiswaId == siswaId && r.TahunAjaranId == tahunAktif.TahunAjaranId);
                if (existing is not null)
                {
                    // SUDAH ADA (mis. sebelumnya 'naik' di TA sama) -> UPDATE jadi 'lulus'
                    // (bukan insert baru - cegah duplikat unique(siswa_id,ta)).
                    existing.Status = StatusRiwayatAkademik.lulus;
                }
                else
                {
                    db.RiwayatAkademik.Add(new RiwayatAkademik { SiswaId = siswaId, TahunAjaranId = tahunAktif.TahunAjaranId, KelasId = siswa.KelasId, Status = StatusRiwayatAkademik.lulus });
                }

                siswa.Status = StatusSiswa.lulus;
                siswa.KelasId = null;
                processed++;
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            TempData["error"] = "Terjadi kesalahan saat memproses kelulusan.";
            return RedirectToAction(nameof(Index));
        }

        TempData["message"] = $"{processed} siswa berhasil diluluskan dan diarsipkan.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("arsip")]
    public async Task<IActionResult> Arsip(int? tahun_ajaran_id, int? kelas_id, string? status)
    {
        var vm = new AkademikArsipViewModel
        {
            TahunAjaranId = tahun_ajaran_id ?? 0,
            KelasId = kelas_id,
            Status = status,
            TahunAjaranList = (await db.TahunAjaran.OrderByDescending(t => t.Nama).Select(t => new { t.TahunAjaranId, t.Nama }).ToListAsync())
                .Select(x => (x.TahunAjaranId, x.Nama)).ToList(),
            KelasList = await db.Kelas.OrderBy(k => k.Tingkat).ThenBy(k => k.NamaKelas).Select(k => new KelasOption { KelasId = k.KelasId, NamaKelas = k.NamaKelas }).ToListAsync(),
        };

        if (tahun_ajaran_id is > 0)
        {
            var query = db.RiwayatAkademik.Include(r => r.Siswa).ThenInclude(s => s.Kelas)
                .Where(r => r.TahunAjaranId == tahun_ajaran_id);
            if (kelas_id is > 0) query = query.Where(r => r.KelasId == kelas_id);
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<StatusRiwayatAkademik>(status, out var st)) query = query.Where(r => r.Status == st);

            var riwayat = await query.OrderBy(r => r.Siswa.Nama).ToListAsync();
            var kelasTujuanMap = await db.Kelas.ToDictionaryAsync(k => k.KelasId, k => k.NamaKelas);

            vm.Riwayat = riwayat.Select(r => new RiwayatAkademikRow
            {
                SiswaId = r.SiswaId,
                Nama = r.Siswa.Nama,
                Nis = r.Siswa.Nis,
                JenisKelamin = r.Siswa.JenisKelamin.ToString(),
                NamaKelas = r.KelasId is not null && kelasTujuanMap.TryGetValue(r.KelasId.Value, out var namaLama) ? namaLama : null,
                Status = r.Status.ToString(),
                // "Naik ke {kelas tujuan}" - kelas SEKARANG siswa (bukan kelas snapshot riwayat)
                KelasTujuanNama = r.Status == StatusRiwayatAkademik.naik ? r.Siswa.Kelas?.NamaKelas : null,
            }).ToList();
        }

        return View(vm);
    }

    [HttpGet("rekap-lulusan")]
    public async Task<IActionResult> RekapLulusan(int? tahun_ajaran_id)
    {
        var vm = new RekapLulusanViewModel
        {
            TahunAjaranId = tahun_ajaran_id ?? 0,
            TahunAjaranList = (await db.TahunAjaran.OrderByDescending(t => t.Nama).Select(t => new { t.TahunAjaranId, t.Nama }).ToListAsync())
                .Select(x => (x.TahunAjaranId, x.Nama)).ToList(),
        };

        if (tahun_ajaran_id is > 0)
        {
            var kelasMap = await db.Kelas.ToDictionaryAsync(k => k.KelasId, k => k.NamaKelas);
            vm.Lulusan = await db.RiwayatAkademik.Include(r => r.Siswa)
                .Where(r => r.TahunAjaranId == tahun_ajaran_id && r.Status == StatusRiwayatAkademik.lulus)
                .OrderBy(r => r.Siswa.Nama)
                .Select(r => new RekapLulusanRow
                {
                    SiswaId = r.SiswaId,
                    Nama = r.Siswa.Nama,
                    Nis = r.Siswa.Nis,
                    JenisKelamin = r.Siswa.JenisKelamin.ToString(),
                    KelasTerakhir = r.KelasId != null ? kelasMap.GetValueOrDefault(r.KelasId.Value) : null,
                    AsalSekolah = r.Siswa.AsalSekolah,
                })
                .ToListAsync();
        }

        return View(vm);
    }

    // KEDUANYA selalu gagal SENGAJA - tidak sentuh DB. Riwayat akademik & rekap
    // lulusan adalah arsip PERMANEN. Toolbar bulk-delete UI "vestigial" - direplikasi
    // apa adanya, bukan bug untuk diperbaiki. Lihat §9 arsipBulkDelete/rekapBulkDelete.
    [HttpPost("arsip/bulk-delete")]
    public IActionResult ArsipBulkDelete()
    {
        TempData["warning"] = "Riwayat akademik tidak dapat dihapus karena dipakai sebagai arsip permanen.";
        return Redirect(Request.Headers.Referer.ToString() is { Length: > 0 } r ? r : Url.Action(nameof(Arsip))!);
    }

    [HttpPost("rekap-lulusan/bulk-delete")]
    public IActionResult RekapBulkDelete()
    {
        TempData["warning"] = "Rekap lulusan tidak dapat dihapus karena dipakai sebagai arsip permanen.";
        return Redirect(Request.Headers.Referer.ToString() is { Length: > 0 } r ? r : Url.Action(nameof(RekapLulusan))!);
    }
}
