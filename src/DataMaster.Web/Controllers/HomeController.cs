using System.Diagnostics;
using DataMaster.Data;
using DataMaster.Data.Entities;
using DataMaster.Web.Models;
using DataMaster.Web.Models.Dashboard;
using DataMaster.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Controllers;

// Port 1:1 dari app/Controllers/Dashboard.php - lihat 04-infra-auth-sync.md §2.
// Route TETAP "/" (Home/Index) bukan "/dashboard" terpisah - PHP asli sendiri
// me-redirect "/" ke "/dashboard" (lihat §4 Home.php), jadi menyajikan dashboard
// langsung dari root di sini menghemat 1 redirect tanpa mengubah perilaku terlihat.
public class HomeController(DataMasterDbContext db, InstallTypeService installType) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (installType.EffectiveInstallType() == "perusahaan") return await IndexPerusahaanAsync();
        return await IndexPendidikanAsync();
    }

    private async Task<IActionResult> IndexPendidikanAsync()
    {
        var vm = new DashboardPendidikanViewModel
        {
            TotalSiswa = await db.Siswa.CountAsync(s => s.Status == StatusSiswa.aktif),
            TotalL = await db.Siswa.CountAsync(s => s.JenisKelamin == JenisKelamin.L && s.Status == StatusSiswa.aktif),
            TotalP = await db.Siswa.CountAsync(s => s.JenisKelamin == JenisKelamin.P && s.Status == StatusSiswa.aktif),
            TotalAlumni = await db.Siswa.CountAsync(s => s.Status == StatusSiswa.lulus),
        };

        vm.RecentSiswa = await db.Siswa.Where(s => s.Status == StatusSiswa.aktif).OrderByDescending(s => s.CreatedAt).Take(5)
            .Select(s => new SiswaTerbaruRow { SiswaId = s.SiswaId, Nis = s.Nis, Nama = s.Nama, JenisKelamin = s.JenisKelamin.ToString(), AsalSekolah = s.AsalSekolah })
            .ToListAsync();

        var tahunAktif = await db.TahunAjaran.FirstOrDefaultAsync(t => t.IsActive);
        vm.TahunAktifNama = tahunAktif?.Nama;

        // perTingkat: akumulasi jumlah siswa aktif per kelas (total_siswa>0), lalu
        // dikelompokkan per label "Kelas {tingkat}" (kode MENTAH, bukan lookup nama
        // TingkatModel - persis Dashboard.php PHP asli, beda dari modul lain yang
        // menampilkan Tingkat.Nama - lihat §2.2).
        var kelasWithCount = await db.Kelas.Where(k => k.IsActive)
            .Select(k => new { k.Tingkat, TotalSiswa = k.SiswaList.Count(s => s.Status == StatusSiswa.aktif) })
            .Where(x => x.TotalSiswa > 0)
            .ToListAsync();
        vm.PerTingkat = kelasWithCount.GroupBy(x => $"Kelas {x.Tingkat}").ToDictionary(g => g.Key, g => g.Sum(x => x.TotalSiswa));

        vm.LangkahPersiapan = await BangunLangkahPersiapanAsync(tahunAktif?.TahunAjaranId ?? 0);

        return View("Index", vm);
    }

    private async Task<List<LangkahPersiapan>> BangunLangkahPersiapanAsync(int taId)
    {
        var langkah = new List<LangkahPersiapan>();

        langkah.Add(new LangkahPersiapan { Nama = "Tahun ajaran aktif", Ok = taId > 0, Url = "/tahun-ajaran", Ket = taId > 0 ? "Sudah diatur." : "Belum ada tahun ajaran aktif." });

        var jmlTingkat = await db.Tingkat.CountAsync(t => t.IsActive);
        langkah.Add(new LangkahPersiapan { Nama = "Tingkat (Kelas 1-6)", Ok = jmlTingkat > 0, Url = "/kelas", Ket = jmlTingkat > 0 ? $"{jmlTingkat} tingkat aktif." : "Belum ada tingkat aktif." });

        var jmlMapel = await db.MataPelajaran.CountAsync();
        var jmlKode = await db.MataPelajaran.CountAsync(m => m.Kode != null && m.Kode != "");
        langkah.Add(new LangkahPersiapan { Nama = "Mata pelajaran + kode huruf", Ok = jmlMapel > 0 && jmlKode > 0, Url = "/kurikulum?tab=mapel", Ket = $"{jmlMapel} mata pelajaran, {jmlKode} punya kode." });

        var jmlJam = taId > 0 ? await db.JamPelajaran.CountAsync(j => j.TahunAjaranId == taId) : 0;
        langkah.Add(new LangkahPersiapan { Nama = "Jam belajar harian", Ok = jmlJam > 0, Url = "/kurikulum?tab=jam", Ket = jmlJam > 0 ? $"{jmlJam} baris jam." : "Belum diatur." });

        var jmlKelas = await db.Kelas.CountAsync(k => k.IsActive);
        langkah.Add(new LangkahPersiapan { Nama = "Kelas (1A, 1B, dst)", Ok = jmlKelas > 0, Url = "/kelas", Ket = jmlKelas > 0 ? $"{jmlKelas} kelas aktif." : "Belum ada kelas aktif." });

        var jmlGuru = await db.Guru.CountAsync(g => g.StatusAktif);
        var jmlNomor = await db.Guru.CountAsync(g => g.StatusAktif && g.NomorUrut != null);
        langkah.Add(new LangkahPersiapan { Nama = "Guru + nomor urut", Ok = jmlGuru > 0 && jmlNomor > 0, Url = "/penugasan-mengajar", Ket = $"{jmlGuru} guru aktif, {jmlNomor} punya nomor urut." });

        var jmlWali = await db.WaliKelas.Where(w => w.Kelas.IsActive).Select(w => w.KelasId).Distinct().CountAsync();
        langkah.Add(new LangkahPersiapan { Nama = "Wali kelas", Ok = jmlKelas > 0 && jmlWali >= jmlKelas, Url = "/kelas", Ket = $"{jmlWali} dari {jmlKelas} kelas sudah punya wali." });

        var jmlJadwal = taId > 0 ? await db.JadwalPelajaran.CountAsync(j => j.TahunAjaranId == taId) : 0;
        langkah.Add(new LangkahPersiapan { Nama = "Jadwal pelajaran", Ok = jmlJadwal > 0, Url = "/jadwal-pelajaran", Ket = jmlJadwal > 0 ? $"{jmlJadwal} slot terisi." : "Belum ada jadwal." });

        return langkah;
    }

    private async Task<IActionResult> IndexPerusahaanAsync()
    {
        var pegawai = await db.Guru.Where(g => g.Jabatan == JabatanGuru.karyawan && g.StatusAktif).OrderBy(g => g.Nama).ToListAsync();
        var vm = new DashboardPerusahaanViewModel
        {
            TotalPegawai = pegawai.Count,
            TotalL = pegawai.Count(p => p.JenisKelamin == JenisKelamin.L),
            TotalP = pegawai.Count(p => p.JenisKelamin == JenisKelamin.P),
            RecentPegawai = pegawai.OrderByDescending(p => p.CreatedAt).Take(5)
                .Select(p => new PegawaiTerbaruRow { GuruId = p.GuruId, Nama = p.Nama, JenisKelamin = p.JenisKelamin?.ToString() ?? "-", NoHandphone = p.NoHandphone })
                .ToList(),
        };
        return View("IndexPerusahaan", vm);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
