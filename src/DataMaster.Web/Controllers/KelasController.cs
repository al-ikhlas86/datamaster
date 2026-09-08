using Microsoft.AspNetCore.Authorization;
using System.Text.RegularExpressions;
using DataMaster.Data;
using DataMaster.Data.Entities;
using DataMaster.Web.Models.Kelas;
using DataMaster.Web.Models.Siswa;
using DataMaster.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Controllers;

// Port 1:1 dari app/Controllers/Kelas.php - lihat 02-guru-kelas-struktur.md §7.
// Penetapan Wali Kelas (assignWaliKelas, autocomplete guru) dilayani endpoint di
// PenugasanMengajarController (persis arsitektur PHP asli - lihat §5.3), View Index
// di sini hanya merender combobox-nya & memanggil endpoint tsb via JS.
[Authorize(Roles = "admin")]
[Route("kelas")]
public class KelasController(DataMasterDbContext db, WaliKelasService waliKelas) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? tahun)
    {
        var aktifId = await db.TahunAjaran.Where(t => t.IsActive).Select(t => t.TahunAjaranId).FirstOrDefaultAsync();
        var taId = tahun ?? aktifId;

        var kelasList = await db.Kelas
            .Select(k => new
            {
                k.KelasId,
                k.NamaKelas,
                k.Tingkat,
                k.IsActive,
                TotalSiswa = k.SiswaList.Count(s => s.Status == StatusSiswa.aktif),
            })
            .OrderBy(k => k.Tingkat).ThenBy(k => k.NamaKelas)
            .ToListAsync();

        var tingkatMaster = await db.Tingkat.ToDictionaryAsync(t => t.Kode);
        var perTingkat = kelasList
            .GroupBy(k => k.Tingkat)
            .Select(g => new TingkatGroup
            {
                Kode = g.Key,
                Label = tingkatMaster.TryGetValue(g.Key, out var t) ? t.Nama : LabelFallback(g.Key),
                TotalSiswa = g.Sum(x => x.TotalSiswa),
                KelasList = g.Select(x => new KelasRow { KelasId = x.KelasId, NamaKelas = x.NamaKelas, Tingkat = x.Tingkat, TotalSiswa = x.TotalSiswa, IsActive = x.IsActive }).ToList(),
            })
            .OrderBy(g => tingkatMaster.TryGetValue(g.Kode, out var t) ? t.Urutan : 99)
            .ThenBy(g => g.Kode)
            .ToList();

        var waliPerKelas = taId > 0
            ? (await waliKelas.GetSemuaDikelompokkanAsync(taId)).ToDictionary(kv => kv.Key, kv => kv.Value.Select(s => new WaliKelasSlot { GuruId = s.GuruId, Nama = s.Nama }).ToList())
            : new Dictionary<int, List<WaliKelasSlot>>();

        var tahunAjaranList = await db.TahunAjaran.OrderByDescending(t => t.Nama).Select(t => new { t.TahunAjaranId, t.Nama }).ToListAsync();

        var vm = new KelasIndexViewModel
        {
            PerTingkat = perTingkat,
            TotalKelas = kelasList.Count,
            TotalSiswa = kelasList.Sum(k => k.TotalSiswa),
            SemuaTingkat = await db.Tingkat.OrderBy(t => t.Urutan).ThenBy(t => t.Kode)
                .Select(t => new TingkatRow { TingkatId = t.TingkatId, Kode = t.Kode, Nama = t.Nama, Urutan = t.Urutan, IsActive = t.IsActive })
                .ToListAsync(),
            WaliPerKelas = waliPerKelas,
            TahunAjaranId = taId,
            TahunAjaranAktif = aktifId,
            TahunAjaranList = tahunAjaranList.Select(x => (x.TahunAjaranId, x.Nama)).ToList(),
        };
        return View(vm);
    }

    [HttpPost("store")]
    public async Task<IActionResult> Store(string? tingkat, string? nama_kelas)
    {
        var tingkatKode = (tingkat ?? "").Trim();
        var namaInput = (nama_kelas ?? "").Trim();

        if (namaInput == "" || namaInput.Length < 3 || namaInput.Length > 100)
        {
            TempData["error"] = "Nama kelas wajib diisi. Nama kelas minimal 3 karakter.";
            return RedirectToAction(nameof(Index));
        }
        if (tingkatKode == "" || !await db.Tingkat.AnyAsync(t => t.Kode == tingkatKode))
        {
            TempData["error"] = "Tingkat tersebut belum terdaftar. Tambahkan dulu di menu Kurikulum & Jam Belajar.";
            return RedirectToAction(nameof(Index));
        }

        var namaKelas = FormatNamaKelas(tingkatKode, namaInput);
        if (namaKelas.Length > 100)
        {
            TempData["error"] = "Nama lengkap kelas maksimal 100 karakter.";
            return RedirectToAction(nameof(Index));
        }
        if (await db.Kelas.AnyAsync(k => k.Tingkat == tingkatKode && k.NamaKelas == namaKelas))
        {
            TempData["error"] = "Nama kelas tersebut sudah dipakai pada tingkat yang sama. Pulihkan kelas arsip atau gunakan nama lain.";
            return RedirectToAction(nameof(Index));
        }

        db.Kelas.Add(new Data.Entities.Kelas { NamaKelas = namaKelas, Tingkat = tingkatKode, IsActive = true });
        await db.SaveChangesAsync();

        TempData["message"] = "Kelas baru berhasil ditambahkan dan akan disinkronkan ke Absen.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var kelas = await db.Kelas.FindAsync(id);
        if (kelas is null) return NotFound("Kelas tidak ditemukan.");
        ViewBag.WaliKelasText = await waliKelas.GetDisplayTextAsync(kelas.KelasId);
        return View(kelas);
    }

    [HttpPost("{id:int}/update")]
    public async Task<IActionResult> Update(int id, string? nama_kelas)
    {
        var kelas = await db.Kelas.FindAsync(id);
        if (kelas is null) return NotFound("Kelas tidak ditemukan.");

        var namaInput = (nama_kelas ?? "").Trim();
        if (namaInput == "" || namaInput.Length < 3 || namaInput.Length > 100)
        {
            ModelState.AddModelError("nama_kelas", "Nama kelas wajib diisi (minimal 3, maksimal 100 karakter).");
            ViewBag.WaliKelasText = await waliKelas.GetDisplayTextAsync(kelas.KelasId);
            return View("Edit", kelas);
        }

        var namaKelas = FormatNamaKelas(kelas.Tingkat, namaInput);
        if (namaKelas.Length > 100)
        {
            TempData["error"] = "Nama lengkap kelas maksimal 100 karakter.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        if (await db.Kelas.AnyAsync(k => k.Tingkat == kelas.Tingkat && k.NamaKelas == namaKelas && k.KelasId != id))
        {
            TempData["error"] = "Nama kelas tersebut sudah digunakan pada tingkat yang sama.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        // wali_kelas SENGAJA tidak diterima dari form ini - lihat catatan lingkup di atas file.
        kelas.NamaKelas = namaKelas;
        await db.SaveChangesAsync();

        TempData["message"] = "Data kelas berhasil diperbarui.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/archive")]
    public async Task<IActionResult> Archive(int id)
    {
        var kelas = await db.Kelas.FindAsync(id);
        if (kelas is null)
        {
            TempData["error"] = "Kelas tidak ditemukan.";
            return RedirectToAction(nameof(Index));
        }

        var siswaAktif = await db.Siswa.CountAsync(s => s.KelasId == id && s.Status == StatusSiswa.aktif);
        if (siswaAktif > 0)
        {
            TempData["error"] = $"Kelas masih dipakai {siswaAktif} siswa aktif. Pindahkan/arsipkan siswanya terlebih dahulu.";
            return RedirectToAction(nameof(Index));
        }

        // Kelas TIDAK dihapus fisik - riwayat presensi/akademik masih membutuhkan identitasnya.
        kelas.IsActive = false;
        kelas.ArchivedAt = DateTime.Now;
        await db.SaveChangesAsync();

        TempData["message"] = "Kelas dipindahkan ke arsip dan akan dinonaktifkan di Absen.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var kelas = await db.Kelas.FindAsync(id);
        if (kelas is null)
        {
            TempData["error"] = "Kelas tidak ditemukan.";
            return RedirectToAction(nameof(Index));
        }
        kelas.IsActive = true;
        kelas.ArchivedAt = null;
        await db.SaveChangesAsync();

        TempData["message"] = "Kelas diaktifkan kembali.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var kelas = await db.Kelas.FindAsync(id);
        if (kelas is null) return NotFound("Kelas tidak ditemukan.");

        var vm = new KelasDetailViewModel
        {
            KelasId = kelas.KelasId,
            NamaKelas = kelas.NamaKelas,
            Tingkat = kelas.Tingkat,
            WaliKelas = await waliKelas.GetDisplayTextAsync(kelas.KelasId),
            SiswaKelas = await db.Siswa.Where(s => s.KelasId == id).OrderBy(s => s.Nama)
                .Select(s => new SiswaRingkas { SiswaId = s.SiswaId, Nama = s.Nama, Nis = s.Nis, JenisKelamin = s.JenisKelamin.ToString() }).ToListAsync(),
            SiswaTanpaKelas = await db.Siswa.Where(s => s.KelasId == null).OrderBy(s => s.Nama)
                .Select(s => new SiswaRingkas { SiswaId = s.SiswaId, Nama = s.Nama, Nis = s.Nis, JenisKelamin = s.JenisKelamin.ToString() }).ToListAsync(),
        };
        return View(vm);
    }

    [HttpPost("{id:int}/add-siswa")]
    public async Task<IActionResult> AddSiswa(int id, List<int>? siswa_ids)
    {
        var kelas = await db.Kelas.FindAsync(id);
        if (kelas is null)
        {
            TempData["error"] = "Kelas tidak ditemukan.";
            return RedirectToAction(nameof(Index));
        }
        var ids = siswa_ids ?? [];
        if (ids.Count == 0)
        {
            TempData["warning"] = "Tidak ada siswa yang dipilih.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var count = await db.Siswa.Where(s => ids.Contains(s.SiswaId))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.KelasId, id));

        TempData["message"] = $"{count} siswa berhasil ditambahkan ke kelas.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{kelasId:int}/remove/{siswaId:int}")]
    public async Task<IActionResult> RemoveSiswa(int kelasId, int siswaId)
    {
        // Langsung update tanpa cek dulu apakah siswa memang di kelas itu - persis PHP asli.
        await db.Siswa.Where(s => s.SiswaId == siswaId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.KelasId, (int?)null));

        TempData["message"] = "Siswa berhasil dikeluarkan dari kelas.";
        return RedirectToAction(nameof(Detail), new { id = kelasId });
    }

    [HttpGet("{id:int}/print")]
    public async Task<IActionResult> PrintKelas(int id)
    {
        var kelas = await db.Kelas.FindAsync(id);
        if (kelas is null) return NotFound("Kelas tidak ditemukan.");

        ViewBag.NamaKelas = kelas.NamaKelas;
        ViewBag.Tingkat = kelas.Tingkat;
        var rows = await db.Siswa.Where(s => s.KelasId == id).OrderBy(s => s.Nama)
            .Select(s => new SiswaPrintRow
            {
                Nis = s.Nis, Nama = s.Nama, JenisKelamin = s.JenisKelamin.ToString(),
                TempatLahir = s.TempatLahir, TanggalLahir = s.TanggalLahir,
                NamaAyah = s.NamaAyah, NoHandphone = s.NoHandphone,
            }).ToListAsync();
        return View("Print", rows);
    }

    [HttpGet("print-tingkat/{tingkat}")]
    public async Task<IActionResult> PrintTingkat(string tingkat)
    {
        var t = await db.Tingkat.FirstOrDefaultAsync(x => x.Kode == tingkat);
        ViewBag.NamaKelas = "Semua " + (t?.Nama ?? LabelFallback(tingkat));
        ViewBag.Tingkat = tingkat;
        var rows = await db.Siswa.Where(s => s.Kelas != null && s.Kelas.Tingkat == tingkat).OrderBy(s => s.Nama)
            .Select(s => new SiswaPrintRow
            {
                Nis = s.Nis, Nama = s.Nama, JenisKelamin = s.JenisKelamin.ToString(),
                NamaKelas = s.Kelas!.NamaKelas, TempatLahir = s.TempatLahir, TanggalLahir = s.TanggalLahir,
                NamaAyah = s.NamaAyah, NoHandphone = s.NoHandphone,
            }).ToListAsync();
        return View("Print", rows);
    }

    // ---------------------------------------------------------------- Helper

    private static string LabelFallback(string kode)
    {
        if (string.IsNullOrEmpty(kode)) return "Semua Tingkat";
        return int.TryParse(kode, out _) ? $"Kelas {kode}" : kode;
    }

    // Port formatNamaKelas() - lihat §7.13. Tingkat non-numerik (mis. "TK-A") dipakai
    // APA ADANYA sbg prefix (TANPA kata "Kelas"). Cegah double-prefix kalau user sudah
    // mengetik nama yang diawali prefix itu sendiri.
    private static string FormatNamaKelas(string tingkat, string nama)
    {
        var namaBersih = Regex.Replace(nama.Trim(), @"\s+", " ");
        var prefix = int.TryParse(tingkat, out _) ? $"Kelas {tingkat}" : tingkat;
        if (Regex.IsMatch(namaBersih, $@"^{Regex.Escape(prefix)}\b", RegexOptions.IgnoreCase))
        {
            return namaBersih;
        }
        return $"{prefix} {namaBersih}".Trim();
    }
}
