using Microsoft.AspNetCore.Authorization;
using System.Text.RegularExpressions;
using DataMaster.Data;
using DataMaster.Data.Entities;
using DataMaster.Web.Models.CalonSiswa;
using DataMaster.Web.Models.Siswa;
using DataMaster.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Controllers;

// Port 1:1 dari app/Controllers/CalonSiswa.php - lihat spec/01-siswa-psb.md §3.17-3.28.
// Mutasi status (Terima/Tolak) SENGAJA didelegasikan ke PsbService (bukan ditulis
// ulang di sini) - persis arsitektur PHP asli (PsbProcessor dipakai SAMA baik dari
// jalur web ini maupun jalur sync Hub API/Mobile-app).
[Authorize(Roles = "admin")]
[Route("calon-siswa")]
public class CalonSiswaController(DataMasterDbContext db, DocumentStorageService docs, PsbService psb, LulusanTkService lulusanTk) : Controller
{
    private static readonly string[] StatusValid = ["menunggu", "diterima", "ditolak", "semua"];

    // ---------------------------------------------------------------- Index

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, string? tingkat)
    {
        var st = StatusValid.Contains(status) ? status! : "menunggu";

        var query = db.CalonSiswa.AsQueryable();
        if (st != "semua") query = query.Where(c => c.Status == Enum.Parse<StatusCalonSiswa>(st));
        if (!string.IsNullOrEmpty(tingkat)) query = query.Where(c => c.TingkatDituju == tingkat);

        // Urut tingkat dulu (kosong di AKHIR), lalu created_at DESC dalam tiap tingkat
        // - lihat §3.17 & §10 poin 12 (raw SQL param3=false di PHP asli).
        var rows = (await query.ToListAsync())
            .OrderBy(c => string.IsNullOrEmpty(c.TingkatDituju) ? 1 : 0)
            .ThenBy(c => c.TingkatDituju)
            .ThenByDescending(c => c.CreatedAt)
            .Select(c => new CalonSiswaRow
            {
                CalonSiswaId = c.CalonSiswaId,
                Nama = c.Nama,
                JenisKelamin = c.JenisKelamin.ToString(),
                TingkatDituju = c.TingkatDituju,
                AsalSekolah = c.AsalSekolah,
                NoHandphone = c.NoHandphone,
                Status = c.Status.ToString(),
                CreatedAt = c.CreatedAt,
            })
            .ToList();

        var vm = new CalonSiswaIndexViewModel
        {
            Status = st,
            Tingkat = tingkat,
            TingkatOptions = await GetDistinctTingkatOptionsAsync(),
            Rows = rows,
        };
        return View(vm);
    }

    // --------------------------------------------------------------- Create

    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        return View(new CalonSiswaFormViewModel { TingkatOptions = await GetDistinctTingkatOptionsAsync() });
    }

    [HttpPost("store")]
    public async Task<IActionResult> Store(CalonSiswaFormInput input)
    {
        var errors = await ValidateFormAsync(input);
        if (errors.Count > 0)
        {
            return View("Create", new CalonSiswaFormViewModel { Input = input, Errors = errors, TingkatOptions = await GetDistinctTingkatOptionsAsync() });
        }

        var calon = new Data.Entities.CalonSiswa
        {
            Nama = input.Nama.Trim(),
            JenisKelamin = Enum.Parse<JenisKelamin>(input.JenisKelamin),
            Nisn = NullIfEmpty(input.Nisn),
            TempatLahir = NullIfEmpty(input.TempatLahir),
            TanggalLahir = input.TanggalLahir,
            AsalSekolah = NullIfEmpty(input.AsalSekolah),
            TingkatDituju = NullIfEmpty(input.TingkatDituju),
            AlamatJalan = NullIfEmpty(input.AlamatJalan),
            AlamatRt = NullIfEmpty(input.AlamatRt),
            AlamatRw = NullIfEmpty(input.AlamatRw),
            AlamatKelurahan = NullIfEmpty(input.AlamatKelurahan),
            AlamatKecamatan = NullIfEmpty(input.AlamatKecamatan),
            NamaAyah = NullIfEmpty(input.NamaAyah),
            PekerjaanAyah = NullIfEmpty(input.PekerjaanAyah),
            NamaIbu = NullIfEmpty(input.NamaIbu),
            PekerjaanIbu = NullIfEmpty(input.PekerjaanIbu),
            NoHandphone = OnlyDigitsOrNull(input.NoHandphone),
            Status = StatusCalonSiswa.menunggu,
        };

        if (input.DokumenKk is { Length: > 0 } f1) calon.DokumenKk = await docs.SaveAsync(f1);
        if (input.DokumenAkta is { Length: > 0 } f2) calon.DokumenAkta = await docs.SaveAsync(f2);
        if (input.DokumenKia is { Length: > 0 } f3) calon.DokumenKia = await docs.SaveAsync(f3);
        if (input.DokumenIjazah is { Length: > 0 } f4) calon.DokumenIjazah = await docs.SaveAsync(f4);

        db.CalonSiswa.Add(calon);
        await db.SaveChangesAsync();

        TempData["message"] = "Calon siswa berhasil ditambahkan.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------- Lulusan TK (#12)

    // Daftar siswa berstatus 'lulus' dari unit sumber yang diizinkan (lihat
    // psb.lulusanLink di Hub API) - PSB TETAP wajib: baris di sini HANYA
    // draft, TU (di sini) yang memutuskan mengimpor jadi Calon Siswa lalu
    // tetap direview/diterima/ditolak lewat alur PSB normal, tidak ada yang
    // otomatis langsung jadi siswa aktif.
    [HttpGet("lulusan-tk")]
    public async Task<IActionResult> LulusanTk()
    {
        var semua = await lulusanTk.AmbilAsync(HttpContext.RequestAborted);
        var sudahDiimpor = (await db.CalonSiswa
                .Where(c => c.SumberLulusanId != null)
                .Select(c => c.SumberLulusanId!.Value)
                .ToListAsync())
            .ToHashSet();

        var vm = semua.Where(r => !sudahDiimpor.Contains(r.Id)).ToList();
        return View(vm);
    }

    [HttpPost("lulusan-tk/{sumberLulusanId:int}/impor")]
    public async Task<IActionResult> ImporLulusanTk(int sumberLulusanId)
    {
        // Cek dobel di sini juga (bukan cuma di tampilan list) - user bisa saja
        // buka 2 tab atau submit ulang form yang sama.
        if (await db.CalonSiswa.AnyAsync(c => c.SumberLulusanId == sumberLulusanId))
        {
            TempData["error"] = "Siswa ini sudah pernah diimpor sebelumnya.";
            return RedirectToAction(nameof(LulusanTk));
        }

        var semua = await lulusanTk.AmbilAsync(HttpContext.RequestAborted);
        var row = semua.FirstOrDefault(r => r.Id == sumberLulusanId);
        if (row is null)
        {
            TempData["error"] = "Data lulusan tidak ditemukan lagi (mungkin sudah ditarik unit lain atau koneksi Hub API bermasalah).";
            return RedirectToAction(nameof(LulusanTk));
        }

        var calon = new Data.Entities.CalonSiswa
        {
            Nama = row.Nama,
            JenisKelamin = Enum.Parse<JenisKelamin>(row.JenisKelamin),
            Nisn = NullIfEmpty(row.Nisn),
            AsalSekolah = "TK Al-Ikhlas 86", // lihat catatan LulusanController.php - unit asal SELALU dari psb.lulusanLink milik sekolah sendiri
            NoHandphone = OnlyDigitsOrNull(row.HpOrtu),
            Status = StatusCalonSiswa.menunggu,
            SumberLulusanId = row.Id,
        };
        db.CalonSiswa.Add(calon);
        await db.SaveChangesAsync();

        TempData["message"] = $"{row.Nama} berhasil diimpor sebagai Calon Siswa. Silakan lengkapi data lain (alamat, dokumen, dll) lalu proses lewat PSB seperti biasa.";
        return RedirectToAction(nameof(Detail), new { id = calon.CalonSiswaId });
    }

    // --------------------------------------------------------------- Detail

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var calon = await db.CalonSiswa.FindAsync(id);
        if (calon is null) return NotFound($"Calon siswa dengan ID {id} tidak ditemukan.");

        var vm = await ToFormViewModelAsync(calon);
        return View(vm);
    }

    private async Task<CalonSiswaFormViewModel> ToFormViewModelAsync(Data.Entities.CalonSiswa calon)
    {
        var vm = new CalonSiswaFormViewModel
        {
            Input = new CalonSiswaFormInput
            {
                CalonSiswaId = calon.CalonSiswaId,
                Nama = calon.Nama,
                JenisKelamin = calon.JenisKelamin.ToString(),
                Nisn = calon.Nisn,
                TempatLahir = calon.TempatLahir,
                TanggalLahir = calon.TanggalLahir,
                AsalSekolah = calon.AsalSekolah,
                TingkatDituju = calon.TingkatDituju,
                AlamatJalan = calon.AlamatJalan,
                AlamatRt = calon.AlamatRt,
                AlamatRw = calon.AlamatRw,
                AlamatKelurahan = calon.AlamatKelurahan,
                AlamatKecamatan = calon.AlamatKecamatan,
                NamaAyah = calon.NamaAyah,
                PekerjaanAyah = calon.PekerjaanAyah,
                NamaIbu = calon.NamaIbu,
                PekerjaanIbu = calon.PekerjaanIbu,
                NoHandphone = calon.NoHandphone,
            },
            DokumenKkLama = calon.DokumenKk,
            DokumenAktaLama = calon.DokumenAkta,
            DokumenKiaLama = calon.DokumenKia,
            DokumenIjazahLama = calon.DokumenIjazah,
            Status = calon.Status.ToString(),
            Catatan = calon.Catatan,
            SiswaIdHasilTerima = calon.SiswaId,
            CreatedAt = calon.CreatedAt,
            TingkatOptions = await GetDistinctTingkatOptionsAsync(),
        };

        // Dropdown kelas utk "Terima" dibatasi ke tingkat tujuan pendaftaran;
        // kosong/tidak ada tingkat -> fallback SEMUA kelas aktif (§3.21).
        var kelasQuery = db.Kelas.Where(k => k.IsActive);
        if (!string.IsNullOrEmpty(calon.TingkatDituju))
        {
            kelasQuery = kelasQuery.Where(k => k.Tingkat == calon.TingkatDituju);
        }
        vm.KelasOptionsUntukTerima = await kelasQuery.OrderBy(k => k.Tingkat).ThenBy(k => k.NamaKelas)
            .Select(k => new KelasOption { KelasId = k.KelasId, NamaKelas = k.NamaKelas }).ToListAsync();
        if (vm.KelasOptionsUntukTerima.Count == 0 && !string.IsNullOrEmpty(calon.TingkatDituju))
        {
            vm.KelasOptionsUntukTerima = await db.Kelas.Where(k => k.IsActive)
                .OrderBy(k => k.Tingkat).ThenBy(k => k.NamaKelas)
                .Select(k => new KelasOption { KelasId = k.KelasId, NamaKelas = k.NamaKelas }).ToListAsync();
        }

        return vm;
    }

    [HttpPost("{id:int}/update")]
    public async Task<IActionResult> Update(int id, CalonSiswaFormInput input)
    {
        var calon = await db.CalonSiswa.FindAsync(id);
        if (calon is null) return NotFound($"Calon siswa dengan ID {id} tidak ditemukan.");

        var errors = await ValidateFormAsync(input, excludeId: id);
        if (errors.Count > 0)
        {
            var vm = await ToFormViewModelAsync(calon);
            vm.Input = input;
            vm.Errors = errors;
            return View("Detail", vm);
        }

        calon.Nama = input.Nama.Trim();
        calon.JenisKelamin = Enum.Parse<JenisKelamin>(input.JenisKelamin);
        calon.Nisn = NullIfEmpty(input.Nisn);
        calon.TempatLahir = NullIfEmpty(input.TempatLahir);
        calon.TanggalLahir = input.TanggalLahir;
        calon.AsalSekolah = NullIfEmpty(input.AsalSekolah);
        calon.TingkatDituju = NullIfEmpty(input.TingkatDituju);
        calon.AlamatJalan = NullIfEmpty(input.AlamatJalan);
        calon.AlamatRt = NullIfEmpty(input.AlamatRt);
        calon.AlamatRw = NullIfEmpty(input.AlamatRw);
        calon.AlamatKelurahan = NullIfEmpty(input.AlamatKelurahan);
        calon.AlamatKecamatan = NullIfEmpty(input.AlamatKecamatan);
        calon.NamaAyah = NullIfEmpty(input.NamaAyah);
        calon.PekerjaanAyah = NullIfEmpty(input.PekerjaanAyah);
        calon.NamaIbu = NullIfEmpty(input.NamaIbu);
        calon.PekerjaanIbu = NullIfEmpty(input.PekerjaanIbu);
        calon.NoHandphone = OnlyDigitsOrNull(input.NoHandphone);

        if (input.DokumenKk is { Length: > 0 } f1) { docs.Delete(calon.DokumenKk); calon.DokumenKk = await docs.SaveAsync(f1); }
        if (input.DokumenAkta is { Length: > 0 } f2) { docs.Delete(calon.DokumenAkta); calon.DokumenAkta = await docs.SaveAsync(f2); }
        if (input.DokumenKia is { Length: > 0 } f3) { docs.Delete(calon.DokumenKia); calon.DokumenKia = await docs.SaveAsync(f3); }
        if (input.DokumenIjazah is { Length: > 0 } f4) { docs.Delete(calon.DokumenIjazah); calon.DokumenIjazah = await docs.SaveAsync(f4); }

        await db.SaveChangesAsync();

        TempData["message"] = "Data calon siswa berhasil diperbarui.";
        return RedirectToAction(nameof(Detail), new { id }); // BUKAN index - lihat §3.22
    }

    // -------------------------------------------------------- Terima/Tolak

    [HttpPost("{id:int}/terima")]
    public async Task<IActionResult> Terima(int id, int? kelas_id)
    {
        var calon = await db.CalonSiswa.FindAsync(id);
        if (calon is null) return NotFound($"Calon siswa dengan ID {id} tidak ditemukan.");

        if (calon.Status != StatusCalonSiswa.menunggu)
        {
            TempData["error"] = "Calon siswa ini sudah diproses sebelumnya.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        if (kelas_id is null or <= 0)
        {
            // PHP: $rules=['kelas_id'=>'required|integer'] TANPA custom message ->
            // jatuh ke default bawaan CI4 (vendor/.../Language/en/Validation.php,
            // TIDAK ada override app/Language/id/) - pesan literal berbahasa Inggris
            // ini SENGAJA direplikasi apa adanya (lihat juga TahunAjaranController
            // utk pola sama), bukan diperhalus ke Indonesia.
            var vm = await ToFormViewModelAsync(calon);
            vm.Errors["KelasTerima"] = "The kelas_id field is required.";
            return View("Detail", vm);
        }

        // NIS SELALU null dari jalur web - auto-generate, sesuai §3.23 poin 3.
        var hasil = await psb.TerimaAsync(id, null, kelas_id.Value);
        if (!hasil.Success)
        {
            TempData["error"] = hasil.Message;
            return RedirectToAction(nameof(Detail), new { id });
        }

        TempData["message"] = hasil.Message;
        return RedirectToAction("Detail", "Siswa", new { id = hasil.SiswaId });
    }

    [HttpPost("{id:int}/tolak")]
    public async Task<IActionResult> Tolak(int id, string? catatan)
    {
        var calon = await db.CalonSiswa.FindAsync(id);
        if (calon is null) return NotFound($"Calon siswa dengan ID {id} tidak ditemukan.");

        var hasil = await psb.TolakAsync(id, catatan);
        if (!hasil.Success)
        {
            TempData["error"] = hasil.Message;
            return RedirectToAction(nameof(Detail), new { id });
        }

        TempData["message"] = hasil.Message;
        return RedirectToAction(nameof(Index)); // index, BUKAN detail - beda dari Terima
    }

    // ------------------------------------------------------- Delete (HARD)

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var calon = await db.CalonSiswa.FindAsync(id);
        if (calon is null) return NotFound($"Calon siswa dengan ID {id} tidak ditemukan.");

        if (calon.Status == StatusCalonSiswa.diterima)
        {
            TempData["error"] = "Calon siswa yang sudah diterima tidak bisa dihapus (sudah jadi data siswa resmi).";
            return RedirectToAction(nameof(Index));
        }

        docs.Delete(calon.DokumenKk);
        docs.Delete(calon.DokumenAkta);
        docs.Delete(calon.DokumenKia);
        docs.Delete(calon.DokumenIjazah);

        db.CalonSiswa.Remove(calon); // HARD DELETE beneran, beda dari Siswa::delete()
        await db.SaveChangesAsync();

        TempData["message"] = $"Data calon siswa '{calon.Nama}' berhasil dihapus.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ Dokumen

    private static readonly Dictionary<string, string> DokumenLabels = new()
    {
        ["DokumenKk"] = "KK",
        ["DokumenAkta"] = "Akta",
        ["DokumenKia"] = "KIA",
        ["DokumenIjazah"] = "Ijazah",
    };

    [HttpGet("{id:int}/dokumen/{field}")]
    public async Task<IActionResult> Dokumen(int id, string field)
    {
        if (!DokumenLabels.TryGetValue(field, out var label)) return NotFound("Dokumen tidak ditemukan.");
        var calon = await db.CalonSiswa.FindAsync(id);
        var namaFile = calon is null ? null : GetDokumenValue(calon, field);
        // Beda dari Siswa: HANYA 1 lokasi folder (tidak ada fallback FCPATH) - §3.28.
        var fullPath = docs.GetFullPath(namaFile);
        if (calon is null || fullPath is null) return NotFound("Dokumen tidak ditemukan.");

        var namaAman = Regex.Replace(calon.Nama, "[^A-Za-z0-9_-]+", "-");
        var ext = Path.GetExtension(fullPath);
        Response.Headers.CacheControl = "private, no-store, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.ContentDisposition = $"inline; filename=\"{label}-Calon-{namaAman}{ext}\"";
        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
        return File(bytes, DocumentStorageService.GetContentType(fullPath));
    }

    [HttpPost("{id:int}/delete-dokumen/{field}")]
    public async Task<IActionResult> DeleteDokumen(int id, string field)
    {
        if (!DokumenLabels.ContainsKey(field))
        {
            TempData["error"] = "Bidang dokumen tidak valid.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        var calon = await db.CalonSiswa.FindAsync(id);
        if (calon is null) return NotFound($"Calon siswa dengan ID {id} tidak ditemukan.");

        var current = GetDokumenValue(calon, field);
        if (string.IsNullOrEmpty(current))
        {
            TempData["error"] = "Dokumen tidak ditemukan atau sudah kosong.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        docs.Delete(current);
        SetDokumenValue(calon, field, null);
        await db.SaveChangesAsync();
        TempData["message"] = "Dokumen berhasil dihapus.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    private static string? GetDokumenValue(Data.Entities.CalonSiswa c, string field) => field switch
    {
        "DokumenKk" => c.DokumenKk,
        "DokumenAkta" => c.DokumenAkta,
        "DokumenKia" => c.DokumenKia,
        "DokumenIjazah" => c.DokumenIjazah,
        _ => null,
    };

    private static void SetDokumenValue(Data.Entities.CalonSiswa c, string field, string? value)
    {
        switch (field)
        {
            case "DokumenKk": c.DokumenKk = value; break;
            case "DokumenAkta": c.DokumenAkta = value; break;
            case "DokumenKia": c.DokumenKia = value; break;
            case "DokumenIjazah": c.DokumenIjazah = value; break;
        }
    }

    // ---------------------------------------------------------------- Helper

    private async Task<List<TingkatOption>> GetDistinctTingkatOptionsAsync()
    {
        var distinctTingkat = await db.Kelas.Where(k => k.IsActive).Select(k => k.Tingkat).Distinct().ToListAsync();
        var master = await db.Tingkat.ToDictionaryAsync(t => t.Kode);
        return distinctTingkat
            .Select(kode => new
            {
                Kode = kode,
                Label = master.TryGetValue(kode, out var t) ? t.Nama : LabelFallback(kode),
                Urutan = master.TryGetValue(kode, out var t2) ? t2.Urutan : 99,
            })
            .OrderBy(x => x.Urutan).ThenBy(x => x.Kode)
            .Select(x => new TingkatOption { Kode = x.Kode, Label = x.Label })
            .ToList();
    }

    // Port TingkatModel::labelFallback() - lihat 03-akademik-jadwal.md §5.8.
    private static string LabelFallback(string kode)
    {
        if (string.IsNullOrEmpty(kode)) return "Semua Tingkat";
        return int.TryParse(kode, out _) ? $"Kelas {kode}" : kode;
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string? OnlyDigitsOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : Regex.Replace(s, @"\D+", "");
    private static bool IsNumericLike(string s) => Regex.IsMatch(s, @"^-?\d+(\.\d+)?$");

    private async Task<Dictionary<string, string>> ValidateFormAsync(CalonSiswaFormInput input, int? excludeId = null)
    {
        var e = new Dictionary<string, string>();

        var nama = (input.Nama ?? "").Trim();
        if (nama == "") e["Nama"] = "Nama wajib diisi.";
        else if (nama.Length < 3) e["Nama"] = "Nama minimal 3 karakter.";
        else if (nama.Length > 100) e["Nama"] = "Nama maksimal 100 karakter.";
        else if (!Regex.IsMatch(nama, ValidationPatterns.RegexNama)) e["Nama"] = "Nama " + ValidationPatterns.PesanRegexNama;

        // Cegah duplikat pendaftaran (2026-09-11, poin #14) - SEBELUMNYA tidak ada
        // pengecekan sama sekali, ortu yang tidak sengaja submit formulir 2x (mis.
        // koneksi lambat, klik dobel) atau daftar ulang anak yang sama tanpa sadar
        // sudah pernah didaftarkan akan membuat 2 baris CalonSiswa utk 1 anak yang
        // sama. Dicocokkan by Nama (case-insensitive) + Tanggal Lahir - HANYA
        // terhadap baris berstatus 'menunggu' (yang SUDAH diterima/ditolak boleh
        // didaftarkan ulang, mis. daftar ulang tahun ajaran lain). TanggalLahir
        // kosong (opsional) -> cek Nama+NoHandphone sbg gantinya, supaya tetap ada
        // sinyal duplikat walau TTL belum diisi.
        if (nama != "" && !e.ContainsKey("Nama"))
        {
            var kandidat = db.CalonSiswa.Where(c => c.Status == StatusCalonSiswa.menunggu && c.Nama.ToLower() == nama.ToLower());
            if (excludeId is not null) kandidat = kandidat.Where(c => c.CalonSiswaId != excludeId);

            var cocok = input.TanggalLahir is not null
                ? await kandidat.AnyAsync(c => c.TanggalLahir == input.TanggalLahir)
                : (!string.IsNullOrWhiteSpace(input.NoHandphone) && await kandidat.AnyAsync(c => c.NoHandphone == OnlyDigitsOrNull(input.NoHandphone)));

            if (cocok) e["Nama"] = $"Sudah ada pendaftaran atas nama \"{nama}\" yang masih menunggu diproses - cek daftar Calon Siswa dulu sebelum mendaftarkan ulang, kalau ini memang anak yang sama.";
        }

        if (input.JenisKelamin != "L" && input.JenisKelamin != "P") e["JenisKelamin"] = "Jenis kelamin wajib dipilih.";

        if (!string.IsNullOrWhiteSpace(input.Nisn))
        {
            if (input.Nisn.Length > 20) e["Nisn"] = "NISN maksimal 20 karakter.";
            else if (!IsNumericLike(input.Nisn)) e["Nisn"] = "NISN hanya boleh berisi angka.";
        }

        if (!string.IsNullOrWhiteSpace(input.TempatLahir))
        {
            if (input.TempatLahir.Length > 100) e["TempatLahir"] = "Tempat lahir maksimal 100 karakter.";
            else if (!Regex.IsMatch(input.TempatLahir, ValidationPatterns.RegexTeksPendek)) e["TempatLahir"] = "Tempat lahir " + ValidationPatterns.PesanRegexTeksPendek;
        }

        if (!string.IsNullOrWhiteSpace(input.AsalSekolah) && input.AsalSekolah.Length > 150) e["AsalSekolah"] = "Asal sekolah maksimal 150 karakter.";

        // tingkat_dituju: harus kode tingkat SUNGGUHAN dipakai kelas aktif (§3.19).
        if (!string.IsNullOrWhiteSpace(input.TingkatDituju))
        {
            if (input.TingkatDituju.Length > 10) e["TingkatDituju"] = "Tingkat dituju maksimal 10 karakter.";
            else
            {
                var validKodes = await db.Kelas.Where(k => k.IsActive).Select(k => k.Tingkat).Distinct().ToListAsync();
                if (!validKodes.Contains(input.TingkatDituju)) e["TingkatDituju"] = "Tingkat dituju tidak dikenali.";
            }
        }

        if (!string.IsNullOrWhiteSpace(input.AlamatJalan) && input.AlamatJalan.Length > 255) e["AlamatJalan"] = "Alamat maksimal 255 karakter.";
        if (!string.IsNullOrWhiteSpace(input.AlamatRt))
        {
            if (input.AlamatRt.Length > 5) e["AlamatRt"] = "RT maksimal 5 karakter.";
            else if (!IsNumericLike(input.AlamatRt)) e["AlamatRt"] = "RT hanya boleh berisi angka.";
        }
        if (!string.IsNullOrWhiteSpace(input.AlamatRw))
        {
            if (input.AlamatRw.Length > 5) e["AlamatRw"] = "RW maksimal 5 karakter.";
            else if (!IsNumericLike(input.AlamatRw)) e["AlamatRw"] = "RW hanya boleh berisi angka.";
        }
        if (!string.IsNullOrWhiteSpace(input.AlamatKelurahan))
        {
            if (input.AlamatKelurahan.Length > 100) e["AlamatKelurahan"] = "Kelurahan maksimal 100 karakter.";
            else if (!Regex.IsMatch(input.AlamatKelurahan, ValidationPatterns.RegexTeksPendek)) e["AlamatKelurahan"] = "Kelurahan " + ValidationPatterns.PesanRegexTeksPendek;
        }
        if (!string.IsNullOrWhiteSpace(input.AlamatKecamatan))
        {
            if (input.AlamatKecamatan.Length > 100) e["AlamatKecamatan"] = "Kecamatan maksimal 100 karakter.";
            else if (!Regex.IsMatch(input.AlamatKecamatan, ValidationPatterns.RegexTeksPendek)) e["AlamatKecamatan"] = "Kecamatan " + ValidationPatterns.PesanRegexTeksPendek;
        }

        if (!string.IsNullOrWhiteSpace(input.NamaAyah))
        {
            if (input.NamaAyah.Length > 100) e["NamaAyah"] = "Nama ayah maksimal 100 karakter.";
            else if (!Regex.IsMatch(input.NamaAyah, ValidationPatterns.RegexNama)) e["NamaAyah"] = "Nama ayah " + ValidationPatterns.PesanRegexNama;
        }
        if (!string.IsNullOrWhiteSpace(input.PekerjaanAyah))
        {
            if (input.PekerjaanAyah.Length > 100) e["PekerjaanAyah"] = "Pekerjaan ayah maksimal 100 karakter.";
            else if (!Regex.IsMatch(input.PekerjaanAyah, ValidationPatterns.RegexTeksPendek)) e["PekerjaanAyah"] = "Pekerjaan ayah " + ValidationPatterns.PesanRegexTeksPendek;
        }
        if (!string.IsNullOrWhiteSpace(input.NamaIbu))
        {
            if (input.NamaIbu.Length > 100) e["NamaIbu"] = "Nama ibu maksimal 100 karakter.";
            else if (!Regex.IsMatch(input.NamaIbu, ValidationPatterns.RegexNama)) e["NamaIbu"] = "Nama ibu " + ValidationPatterns.PesanRegexNama;
        }
        if (!string.IsNullOrWhiteSpace(input.PekerjaanIbu))
        {
            if (input.PekerjaanIbu.Length > 100) e["PekerjaanIbu"] = "Pekerjaan ibu maksimal 100 karakter.";
            else if (!Regex.IsMatch(input.PekerjaanIbu, ValidationPatterns.RegexTeksPendek)) e["PekerjaanIbu"] = "Pekerjaan ibu " + ValidationPatterns.PesanRegexTeksPendek;
        }

        // No Handphone TETAP opsional utk calon siswa (beda dari Siswa!) - §3.19.
        if (!string.IsNullOrWhiteSpace(input.NoHandphone))
        {
            var digits = Regex.Replace(input.NoHandphone, @"\D+", "");
            if (input.NoHandphone.Length > 20) e["NoHandphone"] = "No Handphone maksimal 20 karakter.";
            else if (!IsNumericLike(digits)) e["NoHandphone"] = "No Handphone hanya boleh berisi angka.";
        }

        foreach (var (file, label) in new (IFormFile?, string)[]
                 {
                     (input.DokumenKk, "KK"), (input.DokumenAkta, "Akta"),
                     (input.DokumenKia, "KIA"), (input.DokumenIjazah, "Ijazah"),
                 })
        {
            if (file is null || file.Length == 0) continue;
            if (file.Length > 2 * 1024 * 1024) { e[$"Dokumen{label}"] = $"Ukuran dokumen {label} maksimal 2MB."; continue; }
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext is not (".pdf" or ".jpg" or ".jpeg" or ".png")) { e[$"Dokumen{label}"] = $"Dokumen {label} harus berupa file PDF, JPG, atau PNG."; continue; }
            if (!docs.IsValidDocument(file)) e[$"Dokumen{label}"] = $"Tipe/MIME dokumen {label} tidak valid.";
        }

        return e;
    }
}
