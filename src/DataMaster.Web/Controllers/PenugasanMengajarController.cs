using DataMaster.Data;
using DataMaster.Data.Entities;
using DataMaster.Web.Models.CalonSiswa;
using DataMaster.Web.Models.PenugasanMengajar;
using DataMaster.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Controllers;

// Port 1:1 dari app/Controllers/PenugasanMengajar.php ("Guru Pengampu") - lihat
// 02-guru-kelas-struktur.md §5. Dulu bernama "Wali Kelas & Mata Pelajaran" tapi
// penetapan Wali Kelas sejak 2026-08-27 dipindah ke halaman Kelola Kelas - endpoint
// AssignWaliKelas/CariGuruKelas di controller INI tetap dipanggil dari View Kelas,
// bukan dari halaman Guru Pengampu sendiri (persis arsitektur PHP asli).
[Route("penugasan-mengajar")]
public class PenugasanMengajarController(DataMasterDbContext db, WaliKelasService waliKelas) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var guruList = await db.Guru.Where(g => g.StatusAktif && g.Jabatan != JabatanGuru.karyawan)
            .OrderBy(g => g.NomorUrut == null).ThenBy(g => g.NomorUrut).ThenBy(g => g.Nama)
            .ToListAsync();

        var mapelTaut = await db.GuruMataPelajaran.Include(gm => gm.MataPelajaran)
            .Where(gm => guruList.Select(g => g.GuruId).Contains(gm.GuruId))
            .OrderBy(gm => gm.MataPelajaran.Nama)
            .ToListAsync();

        var vm = new PenugasanMengajarIndexViewModel
        {
            GuruWithMapel = guruList.Select(g => new GuruDenganMapel
            {
                GuruId = g.GuruId,
                Nama = g.Nama,
                Jabatan = g.Jabatan.ToString(),
                NomorUrut = g.NomorUrut,
                Mapel = mapelTaut.Where(gm => gm.GuruId == g.GuruId)
                    .Select(gm => new MapelTaut { GuruMataPelajaranId = gm.GuruMataPelajaranId, MataPelajaranId = gm.MataPelajaranId, Nama = gm.MataPelajaran.Nama, Tingkat = gm.Tingkat })
                    .ToList(),
            }).ToList(),
            MapelDropdown = await db.MataPelajaran.OrderBy(m => m.Nama).Select(m => new MapelOption { MataPelajaranId = m.MataPelajaranId, Nama = m.Nama }).ToListAsync(),
            TingkatOptions = await GetDistinctTingkatOptionsAsync(),
            NomorBerikut = await NomorUrutBerikutnyaAsync(),
        };
        return View(vm);
    }

    private async Task<int> NomorUrutBerikutnyaAsync()
    {
        var dipakai = (await db.Guru.Where(g => g.NomorUrut != null).Select(g => g.NomorUrut!.Value).ToListAsync()).ToHashSet();
        var n = 1;
        while (dipakai.Contains(n)) n++;
        return n;
    }

    // ------------------------------------------- Autocomplete Wali Kelas (dipakai halaman Kelas)

    [HttpGet("cari-guru-kelas")]
    public async Task<IActionResult> CariGuruKelas(string? q, int kecuali, int? tahun)
    {
        var kata = (q ?? "").Trim();
        var ta = tahun ?? await waliKelas.ActiveTahunAjaranIdAsync();

        var semua = await db.Guru.Where(g => g.StatusAktif && (g.Jabatan == JabatanGuru.guru_kelas || g.Jabatan == JabatanGuru.guru_bidang))
            .OrderBy(g => g.Nama).ToListAsync();
        var sudahJadiWali = (await db.WaliKelas.Where(w => w.TahunAjaranId == ta).Select(w => w.GuruId).ToListAsync()).ToHashSet();

        var hasil = semua
            .Where(g => (g.GuruId == kecuali || !sudahJadiWali.Contains(g.GuruId)) && (kata == "" || g.Nama.Contains(kata, StringComparison.OrdinalIgnoreCase)))
            .Take(20)
            .Select(g => new { guru_id = g.GuruId, nama = g.Nama })
            .ToList();
        return Json(hasil);
    }

    [HttpPost("wali-kelas")]
    public async Task<IActionResult> AssignWaliKelas(int kelas_id, List<string>? guru_id, int? tahun_ajaran_id)
    {
        var ajax = Request.Headers.XRequestedWith == "XMLHttpRequest";
        var guruIds = (guru_id ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).Select(int.Parse).ToList();
        var ta = tahun_ajaran_id ?? await waliKelas.ActiveTahunAjaranIdAsync();

        var kelas = await db.Kelas.FindAsync(kelas_id);
        if (kelas is null)
        {
            if (ajax) return StatusCode(422, new { success = false, message = "Kelas tidak ditemukan." });
            TempData["error"] = "Kelas tidak ditemukan.";
            return RedirectToAction("Index", "Kelas");
        }

        IActionResult Kembali() => RedirectToAction("Index", "Kelas", new { tahun = ta });

        if (guruIds.Count != guruIds.Distinct().Count())
        {
            if (ajax) return StatusCode(422, new { success = false, message = "Ada nama wali kelas yang sama dipilih lebih dari sekali - kosongkan salah satunya dulu." });
            TempData["error"] = "Ada nama wali kelas yang sama dipilih lebih dari sekali - kosongkan salah satunya dulu.";
            return Kembali();
        }

        foreach (var gid in guruIds)
        {
            var g = await db.Guru.FindAsync(gid);
            if (g is null || g.Jabatan is not (JabatanGuru.guru_kelas or JabatanGuru.guru_bidang))
            {
                if (ajax) return StatusCode(422, new { success = false, message = "Salah satu wali kelas yang dipilih bukan guru/staf pengajar." });
                TempData["error"] = "Salah satu wali kelas yang dipilih bukan guru/staf pengajar.";
                return Kembali();
            }
        }

        var dilepasDari = await waliKelas.TetapkanSemuaAsync(kelas_id, guruIds, ta);

        if (ajax) return Json(new { success = true });

        string message;
        if (guruIds.Count == 0)
        {
            message = $"Wali kelas {kelas.NamaKelas} dikosongkan.";
        }
        else
        {
            var namaGuru = await db.Guru.Where(g => guruIds.Contains(g.GuruId)).ToDictionaryAsync(g => g.GuruId, g => g.Nama);
            var bagian = guruIds.Select(id => namaGuru.GetValueOrDefault(id, "?") + (dilepasDari.TryGetValue(id, out var lama) ? $" (dilepas dari kelas {lama})" : ""));
            message = $"Wali kelas {kelas.NamaKelas} disimpan: {string.Join(" & ", bagian)}.";
        }
        TempData["message"] = message;
        return Kembali();
    }

    // ------------------------------------------------------------- Guru Pengampu

    public class MapelBaruInput
    {
        public int MataPelajaranId { get; set; }
        public string? Tingkat { get; set; }
    }

    [HttpPost("guru/{guruId:int}/update")]
    public async Task<IActionResult> UpdateBaris(int guruId, string? nomor_urut, List<MapelBaruInput>? mapel_baru)
    {
        var ajax = Request.Headers.XRequestedWith == "XMLHttpRequest";
        var guru = await db.Guru.FindAsync(guruId);
        if (guru is null)
        {
            if (ajax) return StatusCode(422, new { success = false, message = "Guru tidak ditemukan." });
            TempData["error"] = "Guru tidak ditemukan.";
            return RedirectToAction(nameof(Index));
        }

        int? nomorBaru = null;
        var nomorTrim = (nomor_urut ?? "").Trim();
        if (nomorTrim != "")
        {
            if (!int.TryParse(nomorTrim, out var n) || n <= 0)
            {
                var errMsg = "Nomor guru tidak valid (harus angka lebih dari 0).";
                if (ajax) return StatusCode(422, new { success = false, message = errMsg });
                TempData["error"] = errMsg;
                return RedirectToAction(nameof(Index));
            }
            nomorBaru = n;
            var bentrok = await db.Guru.FirstOrDefaultAsync(g => g.NomorUrut == n && g.GuruId != guruId);
            if (bentrok is not null)
            {
                var errMsg = $"Nomor {n} sudah dipakai oleh {bentrok.Nama} - kosongkan/ganti dulu nomor guru itu sebelum dipakai di sini.";
                if (ajax) return StatusCode(422, new { success = false, message = errMsg });
                TempData["error"] = errMsg;
                return RedirectToAction(nameof(Index));
            }
        }

        var validTingkat = (await GetDistinctTingkatOptionsAsync()).Select(t => t.Kode).ToHashSet();
        var errors = new List<string>();
        var mapelBaruHasil = new List<object>();

        await using var tx = await db.Database.BeginTransactionAsync();
        if (nomorBaru is not null) guru.NomorUrut = nomorBaru;

        var ditambahkan = 0;
        foreach (var item in mapel_baru ?? [])
        {
            if (item.MataPelajaranId <= 0) continue;
            if (!string.IsNullOrEmpty(item.Tingkat) && !validTingkat.Contains(item.Tingkat))
            {
                errors.Add($"Tingkat \"{item.Tingkat}\" tidak dikenali.");
                continue;
            }
            var mapel = await db.MataPelajaran.FindAsync(item.MataPelajaranId);
            if (mapel is null) { errors.Add("Mata pelajaran tidak ditemukan."); continue; }

            var tingkatVal = string.IsNullOrEmpty(item.Tingkat) ? null : item.Tingkat;
            var dup = await db.GuruMataPelajaran.AnyAsync(gm => gm.GuruId == guruId && gm.MataPelajaranId == item.MataPelajaranId && gm.Tingkat == tingkatVal);
            if (dup)
            {
                errors.Add($"{guru.Nama} sudah ditautkan ke {mapel.Nama}" + (tingkatVal is not null ? $" tingkat {tingkatVal}" : " (universal)") + ".");
                continue;
            }

            var taut = new GuruMataPelajaran { GuruId = guruId, MataPelajaranId = item.MataPelajaranId, Tingkat = tingkatVal };
            db.GuruMataPelajaran.Add(taut);
            await db.SaveChangesAsync();
            mapelBaruHasil.Add(new { id = taut.GuruMataPelajaranId, nama = mapel.Nama, tingkat = tingkatVal });
            ditambahkan++;
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        if (ajax)
        {
            return Json(new { success = true, errors, ditambahkan, mapelBaru = mapelBaruHasil, nomorUrut = nomorBaru });
        }

        if (errors.Count > 0) TempData["import_errors"] = System.Text.Json.JsonSerializer.Serialize(errors);
        TempData["message"] = $"Data {guru.Nama} disimpan." + (ditambahkan > 0 ? $" {ditambahkan} mata pelajaran baru ditautkan." : "");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("guru-mapel/{id:int}/delete")]
    public async Task<IActionResult> RemoveMapelGuru(int id)
    {
        var taut = await db.GuruMataPelajaran.FindAsync(id);
        if (taut is not null)
        {
            db.GuruMataPelajaran.Remove(taut);
            await db.SaveChangesAsync();
        }
        TempData["message"] = "Tautan mata pelajaran dihapus.";
        return RedirectToAction(nameof(Index));
    }

    // ---------------------------------------------------------------- Helper

    private async Task<List<TingkatOption>> GetDistinctTingkatOptionsAsync()
    {
        var distinctTingkat = await db.Kelas.Where(k => k.IsActive).Select(k => k.Tingkat).Distinct().ToListAsync();
        var master = await db.Tingkat.ToDictionaryAsync(t => t.Kode);
        return distinctTingkat
            .Select(kode => new { Kode = kode, Label = master.TryGetValue(kode, out var t) ? t.Nama : LabelFallback(kode), Urutan = master.TryGetValue(kode, out var t2) ? t2.Urutan : 99 })
            .OrderBy(x => x.Urutan).ThenBy(x => x.Kode)
            .Select(x => new TingkatOption { Kode = x.Kode, Label = x.Label })
            .ToList();
    }

    private static string LabelFallback(string kode)
    {
        if (string.IsNullOrEmpty(kode)) return "Semua Tingkat";
        return int.TryParse(kode, out _) ? $"Kelas {kode}" : kode;
    }
}
