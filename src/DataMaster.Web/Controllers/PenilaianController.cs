using DataMaster.Data;
using DataMaster.Data.Entities;
using DataMaster.Web.Models.Penilaian;
using DataMaster.Web.Models.Siswa;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Controllers;

// Input nilai sikap (perilaku) per semester - dasar fitur acak kenaikan kelas
// di AkademikController. Baru (2026-09-25), TIDAK ada di app PHP asli.
[Authorize(Roles = "admin")]
[Route("penilaian-sikap")]
public class PenilaianController(DataMasterDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? kelas_id)
    {
        var tahunAktif = await db.TahunAjaran.FirstOrDefaultAsync(t => t.IsActive);
        var vm = new PenilaianSikapIndexViewModel { AdaTahunAktif = tahunAktif is not null, TahunAktifNama = tahunAktif?.Nama };
        if (tahunAktif is null) return View(vm);

        vm.TahunAjaranId = tahunAktif.TahunAjaranId;
        vm.KelasList = await db.Kelas.Where(k => k.IsActive).OrderBy(k => k.Tingkat).ThenBy(k => k.NamaKelas)
            .Select(k => new KelasOption { KelasId = k.KelasId, NamaKelas = k.NamaKelas }).ToListAsync();
        vm.KelasId = kelas_id;

        if (kelas_id is > 0)
        {
            var siswaList = await db.Siswa.Where(s => s.KelasId == kelas_id && s.Status == StatusSiswa.aktif).OrderBy(s => s.Nama).ToListAsync();
            var siswaIds = siswaList.Select(s => s.SiswaId).ToList();
            var nilaiMap = await db.PenilaianSikap
                .Where(p => siswaIds.Contains(p.SiswaId) && p.TahunAjaranId == tahunAktif.TahunAjaranId)
                .ToListAsync();

            vm.Siswa = siswaList.Select(s => new SiswaSikapRow
            {
                SiswaId = s.SiswaId,
                Nama = s.Nama,
                Nis = s.Nis,
                GradeGanjil = nilaiMap.FirstOrDefault(p => p.SiswaId == s.SiswaId && p.Semester == Semester.ganjil)?.Grade.ToString(),
                GradeGenap = nilaiMap.FirstOrDefault(p => p.SiswaId == s.SiswaId && p.Semester == Semester.genap)?.Grade.ToString(),
            }).ToList();
        }

        return View(vm);
    }

    public class SimpanRow
    {
        public int SiswaId { get; set; }
        public string? GradeGanjil { get; set; }
        public string? GradeGenap { get; set; }
    }

    [HttpPost("simpan")]
    public async Task<IActionResult> Simpan(int kelas_id, List<SimpanRow>? rows)
    {
        var tahunAktif = await db.TahunAjaran.FirstOrDefaultAsync(t => t.IsActive);
        if (tahunAktif is null)
        {
            TempData["error"] = "Tidak ada tahun ajaran aktif.";
            return RedirectToAction(nameof(Index), new { kelas_id });
        }

        var siswaIds = (rows ?? []).Select(r => r.SiswaId).ToList();
        var existing = await db.PenilaianSikap.Where(p => siswaIds.Contains(p.SiswaId) && p.TahunAjaranId == tahunAktif.TahunAjaranId).ToListAsync();

        void Upsert(int siswaId, Semester semester, string? gradeStr)
        {
            if (string.IsNullOrEmpty(gradeStr) || !Enum.TryParse<GradeSikap>(gradeStr, out var grade)) return;
            var row = existing.FirstOrDefault(p => p.SiswaId == siswaId && p.Semester == semester);
            if (row is null)
            {
                db.PenilaianSikap.Add(new PenilaianSikap { SiswaId = siswaId, TahunAjaranId = tahunAktif.TahunAjaranId, Semester = semester, Grade = grade, UpdatedAt = DateTime.Now, CreatedAt = DateTime.Now });
            }
            else if (row.Grade != grade)
            {
                row.Grade = grade;
                row.UpdatedAt = DateTime.Now;
            }
        }

        foreach (var r in rows ?? [])
        {
            Upsert(r.SiswaId, Semester.ganjil, r.GradeGanjil);
            Upsert(r.SiswaId, Semester.genap, r.GradeGenap);
        }

        await db.SaveChangesAsync();
        TempData["message"] = "Nilai sikap tersimpan.";
        return RedirectToAction(nameof(Index), new { kelas_id });
    }
}
