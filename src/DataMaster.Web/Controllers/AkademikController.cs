using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using DataMaster.Data;
using DataMaster.Data.Entities;
using DataMaster.Web.Models.Akademik;
using DataMaster.Web.Models.Siswa;
using DataMaster.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Controllers;

// Port 1:1 dari app/Controllers/Akademik.php - lihat 03-akademik-jadwal.md §9.
// BEDA TOTAL dari Kurikulum/Jadwal/Kalender - modul ini murni proses kenaikan
// kelas & kelulusan akhir tahun ajaran, arsip historis, dan rekap lulusan.
//
// Tahun Ajaran Kerja (2026-09-25, lihat TahunAjaranKerjaService): kalau TU
// sedang kerja di tahun ajaran yang BEDA dari yang aktif ("mode persiapan"),
// tombol Proses Naik Kelas/Kelulusan/Acak TIDAK langsung mengubah Siswa.KelasId -
// cuma menyusun RencanaKenaikan yang baru BENERAN diterapkan oleh
// TahunAjaranController.SetActive saat tahun itu diaktifkan. Saat TU kerja normal
// (tahun kerja == tahun aktif, kondisi sehari-hari), semua tombol jalan PERSIS
// seperti sebelum fitur ini ada - lihat KenaikanKelasService utk logic bersama.
[Authorize(Roles = "admin")]
[Route("akademik")]
public class AkademikController(DataMasterDbContext db, TahunAjaranKerjaService tahunAjaranKerja, KenaikanKelasService kenaikanKelas) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var tahunAktif = await db.TahunAjaran.FirstOrDefaultAsync(t => t.IsActive);
        var vm = new AkademikIndexViewModel { AdaTahunAktif = tahunAktif is not null, TahunAktifNama = tahunAktif?.Nama };
        if (tahunAktif is null) return View(vm);

        var takId = await tahunAjaranKerja.GetKerjaIdAsync();
        vm.ModePersiapan = takId != tahunAktif.TahunAjaranId;
        vm.TahunKerjaNama = vm.ModePersiapan ? (await db.TahunAjaran.FindAsync(takId))?.Nama : tahunAktif.Nama;

        var siswaAktif = await db.Siswa.Include(s => s.Kelas).Where(s => s.Status == StatusSiswa.aktif).OrderBy(s => s.Nama).ToListAsync();
        var tingkatMaster = await db.Tingkat.ToDictionaryAsync(t => t.Kode);
        var gradeMap = await HitungGradeKumulatifAsync(tahunAktif.TahunAjaranId, siswaAktif.Select(s => s.SiswaId).ToList());

        var rencanaMap = vm.ModePersiapan
            ? await db.RencanaKenaikan.Where(r => r.TahunAjaranTujuanId == takId).ToDictionaryAsync(r => r.SiswaId)
            : [];
        var kelasNamaMap = vm.ModePersiapan ? await db.Kelas.ToDictionaryAsync(k => k.KelasId, k => k.NamaKelas) : [];

        string? LabelRencana(int siswaId)
        {
            if (!rencanaMap.TryGetValue(siswaId, out var r)) return null;
            if (r.Lulus) return "Rencana: Lulus";
            var namaKelas = r.KelasTujuanId is { } kid && kelasNamaMap.TryGetValue(kid, out var n) ? n : "?";
            return $"Rencana: Naik ke {namaKelas}";
        }

        vm.KelompokSiswa = siswaAktif
            .GroupBy(s => s.KelasId)
            .Select(g => new KelompokKelasSiswa
            {
                KelasId = g.Key,
                NamaKelas = g.Key is null ? "Tanpa Kelas" : g.First().Kelas!.NamaKelas,
                Tingkat = g.Key is null ? "" : g.First().Kelas!.Tingkat,
                SiswaList = g.Select(s => new SiswaAktifRow
                {
                    SiswaId = s.SiswaId,
                    Nama = s.Nama,
                    Nis = s.Nis,
                    JenisKelamin = s.JenisKelamin.ToString(),
                    GradeSikapKumulatif = gradeMap[s.SiswaId] is { } g2 ? LabelGrade(g2) : null,
                    RencanaLabel = LabelRencana(s.SiswaId),
                }).ToList(),
            })
            .OrderBy(k => k.KelasId is null ? 99 : (tingkatMaster.TryGetValue(k.Tingkat, out var t) ? t.Urutan : 98))
            .ToList();

        vm.KelasList = await db.Kelas.Where(k => k.IsActive).OrderBy(k => k.Tingkat).ThenBy(k => k.NamaKelas)
            .Select(k => new KelasOption { KelasId = k.KelasId, NamaKelas = k.NamaKelas }).ToListAsync();

        return View(vm);
    }

    // Upsert 1 baris RencanaKenaikan (dipanggil dari jalur "mode persiapan" di
    // ProsesNaikKelas/ProsesKelulusan/ProsesRandomKenaikan) - REVISI kalau siswa
    // itu sudah punya rencana utk tahun tujuan yang sama, bukan numpuk baris.
    private async Task SimpanRencanaAsync(int tahunAjaranTujuanId, int siswaId, bool lulus, int? kelasTujuanId)
    {
        var existing = await db.RencanaKenaikan.FirstOrDefaultAsync(r => r.SiswaId == siswaId && r.TahunAjaranTujuanId == tahunAjaranTujuanId);
        if (existing is null)
        {
            db.RencanaKenaikan.Add(new RencanaKenaikan { SiswaId = siswaId, TahunAjaranTujuanId = tahunAjaranTujuanId, Lulus = lulus, KelasTujuanId = kelasTujuanId, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now });
        }
        else
        {
            existing.Lulus = lulus;
            existing.KelasTujuanId = kelasTujuanId;
            existing.UpdatedAt = DateTime.Now;
        }
    }

    private static string LabelGrade(GradeSikap g) => g switch
    {
        GradeSikap.sangat_baik => "Sangat Baik",
        GradeSikap.baik => "Baik",
        GradeSikap.cukup => "Cukup",
        GradeSikap.perlu_bimbingan => "Perlu Bimbingan",
        _ => g.ToString(),
    };

    // Kumulatif = rata2 grade (dibulatkan) dari semester ganjil+genap yang SUDAH
    // diisi utk TA ini - siswa yang baru diisi 1 semester tetap dapat nilai (pakai
    // semester itu saja), yang belum diisi sama sekali -> null ("belum dinilai",
    // masuk grup acak biasa di GenerateRandomKenaikan, BUKAN grup prioritas sebar).
    private async Task<Dictionary<int, GradeSikap?>> HitungGradeKumulatifAsync(int tahunAjaranId, List<int> siswaIds)
    {
        var nilai = await db.PenilaianSikap.Where(p => siswaIds.Contains(p.SiswaId) && p.TahunAjaranId == tahunAjaranId).ToListAsync();
        var result = new Dictionary<int, GradeSikap?>();
        foreach (var id in siswaIds)
        {
            var milikSiswa = nilai.Where(p => p.SiswaId == id).ToList();
            result[id] = milikSiswa.Count == 0 ? null : (GradeSikap)(int)Math.Round(milikSiswa.Average(p => (int)p.Grade), MidpointRounding.AwayFromZero);
        }
        return result;
    }

    private static void Shuffle<T>(List<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private const string RandomKenaikanSessionKey = "AkademikRandomKenaikanProposal";

    // Acak kenaikan kelas berbasis nilai sikap (2026-09-25, permintaan user):
    // siswa grade "Perlu Bimbingan" (terburuk) diproses round-robin PALING AWAL,
    // satu-satu bergantian ke tiap kelas tujuan supaya tidak numpuk 1 kelas -
    // sisanya (grade lain + belum dinilai) diacak murni lalu lanjut round-robin
    // dari posisi terakhir (bukan reset) supaya jumlah akhir tiap kelas tetap
    // merata. Hasil TIDAK langsung commit - disimpan di Session lalu diarahkan
    // ke halaman review (ReviewRandomKenaikan) supaya TU bisa cek/ubah manual
    // dulu sebelum ACC, persis pola preview import Siswa/Guru di controller lain.
    [HttpPost("generate-random-kenaikan")]
    public async Task<IActionResult> GenerateRandomKenaikan(List<int>? siswa_ids, List<int>? kelas_tujuan_acak)
    {
        var tahunAktif = await db.TahunAjaran.FirstOrDefaultAsync(t => t.IsActive);
        if (tahunAktif is null)
        {
            TempData["error"] = "Tidak ada tahun ajaran aktif. Silakan aktifkan terlebih dahulu.";
            return RedirectToAction(nameof(Index));
        }

        var ids = (siswa_ids ?? []).Distinct().ToList();
        var kelasTujuanIds = (kelas_tujuan_acak ?? []).Distinct().ToList();
        if (ids.Count == 0)
        {
            TempData["warning"] = "Tidak ada siswa yang dipilih.";
            return RedirectToAction(nameof(Index));
        }
        if (kelasTujuanIds.Count == 0)
        {
            TempData["error"] = "Pilih minimal 1 kelas tujuan untuk diacak.";
            return RedirectToAction(nameof(Index));
        }

        var siswaList = await db.Siswa.Where(s => ids.Contains(s.SiswaId) && s.Status == StatusSiswa.aktif).OrderBy(s => s.Nama).ToListAsync();
        var gradeMap = await HitungGradeKumulatifAsync(tahunAktif.TahunAjaranId, siswaList.Select(s => s.SiswaId).ToList());

        var grupBuruk = siswaList.Where(s => gradeMap[s.SiswaId] == GradeSikap.perlu_bimbingan).ToList();
        var grupSisa = siswaList.Where(s => gradeMap[s.SiswaId] != GradeSikap.perlu_bimbingan).ToList();

        var rng = new Random();
        Shuffle(grupBuruk, rng);
        Shuffle(grupSisa, rng);

        var hasil = new List<RandomKenaikanProposalRow>();
        var idx = 0;
        foreach (var s in grupBuruk.Concat(grupSisa))
        {
            var kelasTujuan = kelasTujuanIds[idx % kelasTujuanIds.Count];
            idx++;
            hasil.Add(new RandomKenaikanProposalRow
            {
                SiswaId = s.SiswaId,
                Nama = s.Nama,
                Nis = s.Nis,
                GradeSikapKumulatif = gradeMap[s.SiswaId] is { } g ? LabelGrade(g) : null,
                KelasTujuanTerpilih = kelasTujuan,
            });
        }

        HttpContext.Session.SetString(RandomKenaikanSessionKey, JsonSerializer.Serialize(hasil));
        HttpContext.Session.SetString(RandomKenaikanSessionKey + "_kelas", JsonSerializer.Serialize(kelasTujuanIds));
        return RedirectToAction(nameof(ReviewRandomKenaikan));
    }

    [HttpGet("review-random-kenaikan")]
    public async Task<IActionResult> ReviewRandomKenaikan()
    {
        var json = HttpContext.Session.GetString(RandomKenaikanSessionKey);
        var kelasJson = HttpContext.Session.GetString(RandomKenaikanSessionKey + "_kelas");
        if (json is null || kelasJson is null)
        {
            TempData["error"] = "Belum ada hasil acak untuk direview. Silakan acak ulang.";
            return RedirectToAction(nameof(Index));
        }

        var proposal = JsonSerializer.Deserialize<List<RandomKenaikanProposalRow>>(json) ?? [];
        var kelasTujuanIds = JsonSerializer.Deserialize<List<int>>(kelasJson) ?? [];
        var kelasList = await db.Kelas.Where(k => kelasTujuanIds.Contains(k.KelasId)).OrderBy(k => k.NamaKelas)
            .Select(k => new KelasOption { KelasId = k.KelasId, NamaKelas = k.NamaKelas }).ToListAsync();

        var (modePersiapan, tahunKerjaNama) = await CekModePersiapanAsync();
        return View(new RandomKenaikanReviewViewModel { Proposal = proposal, KelasTujuanList = kelasList, ModePersiapan = modePersiapan, TahunKerjaNama = tahunKerjaNama });
    }

    public class ProsesRandomKenaikanRow
    {
        public int SiswaId { get; set; }
        public int KelasTujuan { get; set; }
    }

    // (ModePersiapan, TahunKerjaNama) - dipakai semua tombol proses di controller ini
    // utk tau harus langsung terap (KenaikanKelasService) atau simpan sbg rencana
    // (SimpanRencanaAsync). Lihat komentar kelas di atas & TahunAjaranKerjaService.
    private async Task<(bool ModePersiapan, string? TahunKerjaNama)> CekModePersiapanAsync()
    {
        var tahunAktif = await db.TahunAjaran.FirstOrDefaultAsync(t => t.IsActive);
        if (tahunAktif is null) return (false, null);
        var takId = await tahunAjaranKerja.GetKerjaIdAsync();
        if (takId == tahunAktif.TahunAjaranId) return (false, tahunAktif.Nama);
        var tak = await db.TahunAjaran.FindAsync(takId);
        return (true, tak?.Nama);
    }

    // Commit final - baca dari body form hasil review (yang mungkin sudah diubah
    // manual TU lewat dropdown per baris), BUKAN dari Session lagi - Session cuma
    // dipakai utk lompat Generate -> halaman Review (lihat GenerateRandomKenaikan).
    [HttpPost("proses-random-kenaikan")]
    public async Task<IActionResult> ProsesRandomKenaikan(List<ProsesRandomKenaikanRow>? rows)
    {
        var tahunAktif = await db.TahunAjaran.FirstOrDefaultAsync(t => t.IsActive);
        if (tahunAktif is null)
        {
            TempData["error"] = "Tidak ada tahun ajaran aktif. Silakan aktifkan terlebih dahulu.";
            return RedirectToAction(nameof(Index));
        }
        var list = rows ?? [];
        if (list.Count == 0)
        {
            TempData["warning"] = "Tidak ada data untuk diproses.";
            return RedirectToAction(nameof(Index));
        }

        var takId = await tahunAjaranKerja.GetKerjaIdAsync();
        var modePersiapan = takId != tahunAktif.TahunAjaranId;

        await using var tx = await db.Database.BeginTransactionAsync();
        int processed;
        try
        {
            if (modePersiapan)
            {
                processed = 0;
                foreach (var row in list)
                {
                    if (row.KelasTujuan <= 0) continue;
                    await SimpanRencanaAsync(takId, row.SiswaId, lulus: false, row.KelasTujuan);
                    processed++;
                }
                await db.SaveChangesAsync();
            }
            else
            {
                processed = await kenaikanKelas.TerapkanAsync(tahunAktif.TahunAjaranId, list.Where(r => r.KelasTujuan > 0).Select(r => (r.SiswaId, (int?)r.KelasTujuan, false)).ToList());
            }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            TempData["error"] = "Terjadi kesalahan saat memproses kenaikan kelas acak.";
            return RedirectToAction(nameof(Index));
        }

        HttpContext.Session.Remove(RandomKenaikanSessionKey);
        HttpContext.Session.Remove(RandomKenaikanSessionKey + "_kelas");
        TempData["message"] = modePersiapan
            ? $"{processed} siswa tersimpan sbg rencana kenaikan (acak) - akan diterapkan otomatis saat tahun ajaran itu diaktifkan."
            : $"{processed} siswa berhasil dinaikkan kelas (acak berbasis nilai sikap).";
        return RedirectToAction(nameof(Index));
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

        var takId = await tahunAjaranKerja.GetKerjaIdAsync();
        var modePersiapan = takId != tahunAktif.TahunAjaranId;

        await using var tx = await db.Database.BeginTransactionAsync();
        int processed;
        try
        {
            if (modePersiapan)
            {
                processed = 0;
                foreach (var siswaId in ids)
                {
                    await SimpanRencanaAsync(takId, siswaId, lulus: false, kelas_tujuan);
                    processed++;
                }
                await db.SaveChangesAsync();
            }
            else
            {
                // Ditemukan via audit performa (2026-09-08): dioptimasi jadi muat-semua di
                // awal (di dalam KenaikanKelasService), bukan query per-siswa di dalam loop.
                processed = await kenaikanKelas.TerapkanAsync(tahunAktif.TahunAjaranId, ids.Select(id => (id, (int?)kelas_tujuan, false)).ToList());
            }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            TempData["error"] = "Terjadi kesalahan saat memproses kenaikan kelas.";
            return RedirectToAction(nameof(Index));
        }

        TempData["message"] = modePersiapan
            ? $"{processed} siswa tersimpan sbg rencana naik kelas - akan diterapkan otomatis saat tahun ajaran itu diaktifkan."
            : $"{processed} siswa berhasil dinaikkan kelas.";
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

        var takId = await tahunAjaranKerja.GetKerjaIdAsync();
        var modePersiapan = takId != tahunAktif.TahunAjaranId;

        await using var tx = await db.Database.BeginTransactionAsync();
        int processed;
        try
        {
            if (modePersiapan)
            {
                processed = 0;
                foreach (var siswaId in ids)
                {
                    await SimpanRencanaAsync(takId, siswaId, lulus: true, kelasTujuanId: null);
                    processed++;
                }
                await db.SaveChangesAsync();
            }
            else
            {
                processed = await kenaikanKelas.TerapkanAsync(tahunAktif.TahunAjaranId, ids.Select(id => (id, (int?)null, true)).ToList());
            }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            TempData["error"] = "Terjadi kesalahan saat memproses kelulusan.";
            return RedirectToAction(nameof(Index));
        }

        TempData["message"] = modePersiapan
            ? $"{processed} siswa tersimpan sbg rencana kelulusan - akan diterapkan otomatis saat tahun ajaran itu diaktifkan."
            : $"{processed} siswa berhasil diluluskan dan diarsipkan.";
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
