using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DataMaster.Data;
using DataMaster.Data.Entities;
using DataMaster.Web.Models.JadwalPelajaran;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Controllers;

// Port 1:1 dari app/Controllers/JadwalPelajaran.php - lihat 03-akademik-jadwal.md §4.
// GRID: baris=jam ke-N (union semua hari), kolom=hari. Kode sel "35K" = guru nomor 35
// mengajar mapel berkode K. Kode tanpa angka ("F") = mapel TANPA guru tetap (BTAQ Ummi
// berkelompok - FITUR, bukan bug). Sel dikosongkan = hapus slot.
[Route("jadwal-pelajaran")]
public class JadwalPelajaranController(DataMasterDbContext db) : Controller
{
    // -------------------------------------------------------------- Konteks

    private async Task<(int TahunAjaranId, string Semester)> KonteksAsync(int? tahun, string? semester)
    {
        var ta = tahun ?? await db.TahunAjaran.Where(t => t.IsActive).Select(t => t.TahunAjaranId).FirstOrDefaultAsync();
        var sem = semester is "genap" ? "genap" : "ganjil";
        return (ta, sem);
    }

    private async Task<Dictionary<string, Data.Entities.Tingkat>> TingkatMasterAsync() => await db.Tingkat.ToDictionaryAsync(t => t.Kode);

    // -------------------------------------------------------------- Index (grid)

    [HttpGet("")]
    public async Task<IActionResult> Index(int? tahun, string? semester, int? kelas)
    {
        var (taId, sem) = await KonteksAsync(tahun, semester);
        var vm = new JadwalPelajaranIndexViewModel
        {
            TahunAjaranId = taId,
            Semester = sem,
            TahunAjaranList = (await db.TahunAjaran.OrderByDescending(t => t.Nama).Select(t => new { t.TahunAjaranId, t.Nama }).ToListAsync())
                .Select(x => (x.TahunAjaranId, x.Nama)).ToList(),
        };
        vm.AdaTahunAjaran = vm.TahunAjaranList.Count > 0;
        if (!vm.AdaTahunAjaran || taId <= 0) return View(vm);

        var tingkatMaster = await TingkatMasterAsync();
        var kelasEntities = await db.Kelas.Where(k => k.IsActive).ToListAsync();
        vm.KelasList = kelasEntities
            .OrderBy(k => tingkatMaster.TryGetValue(k.Tingkat, out var t) ? t.Urutan : 99).ThenBy(k => k.NamaKelas)
            .Select(k => new KelasOpt { KelasId = k.KelasId, NamaKelas = k.NamaKelas, Tingkat = k.Tingkat })
            .ToList();
        vm.AdaKelasAktif = vm.KelasList.Count > 0;

        var jamSemua = await db.JamPelajaran.Where(j => j.TahunAjaranId == taId).ToListAsync();
        vm.AdaJamBelajar = jamSemua.Count > 0;
        if (!vm.AdaKelasAktif || !vm.AdaJamBelajar) return View(vm);

        vm.KelasId = kelas is > 0 && vm.KelasList.Any(k => k.KelasId == kelas) ? kelas : vm.KelasList[0].KelasId;
        var kelasAktif = vm.KelasList.First(k => k.KelasId == vm.KelasId);

        vm.HariAktif = HariUrut.Where(h => jamSemua.Any(j => j.Hari.ToString() == h)).ToArray();
        var semuaJamKe = jamSemua.Select(j => j.JamKe).Distinct().OrderBy(x => x).ToList();

        var batasan = await db.JamPelajaranTingkat.ToListAsync();
        var batasanPerJam = batasan.GroupBy(b => b.JamPelajaranId).ToDictionary(g => g.Key, g => g.Select(b => b.TingkatKode).ToHashSet());

        var isiKelas = await db.JadwalPelajaran.Include(j => j.MataPelajaran).Include(j => j.Guru)
            .Where(j => j.TahunAjaranId == taId && j.Semester.ToString() == sem && j.KelasId == vm.KelasId)
            .ToListAsync();
        var isiByJam = isiKelas.ToDictionary(j => j.JamPelajaranId);

        vm.Grid = semuaJamKe.Select(jk =>
        {
            var baris = new GridBaris { JamKe = jk };
            string? waktuLabel = null;
            foreach (var h in vm.HariAktif)
            {
                var jam = jamSemua.FirstOrDefault(j => j.Hari.ToString() == h && j.JamKe == jk);
                if (jam is null) { baris.PerHari[h] = new GridSel { JamPelajaranId = null }; continue; }
                waktuLabel ??= $"{jam.JamMulai:HH:mm}-{jam.JamSelesai:HH:mm}";
                var dibatasi = batasanPerJam.TryGetValue(jam.JamPelajaranId, out var set) && set.Count > 0 && !set.Contains(kelasAktif.Tingkat);
                string? kode = null;
                if (jam.Jenis == JenisJamPelajaran.pelajaran && isiByJam.TryGetValue(jam.JamPelajaranId, out var isi))
                {
                    var nomor = isi.Guru?.NomorUrut;
                    kode = (nomor is not null ? nomor.ToString() : "") + (isi.MataPelajaran.Kode ?? "");
                }
                baris.PerHari[h] = new GridSel
                {
                    JamPelajaranId = jam.JamPelajaranId,
                    Jenis = jam.Jenis.ToString(),
                    Label = jam.Label,
                    DibatasiTingkatLain = dibatasi,
                    Kode = kode,
                };
            }
            baris.WaktuLabel = waktuLabel;
            return baris;
        }).ToList();

        // Panel Progres Kurikulum
        var alokasi = await db.KurikulumAlokasi.Where(a => a.TahunAjaranId == taId && a.TingkatKode == kelasAktif.Tingkat).ToListAsync();
        var terpakai = isiKelas.GroupBy(j => j.MataPelajaranId).ToDictionary(g => g.Key, g => g.Count());
        var mapelMaster = await db.MataPelajaran.ToDictionaryAsync(m => m.MataPelajaranId);
        var mapelIds = alokasi.Select(a => a.MataPelajaranId).Union(terpakai.Keys).Distinct();
        vm.Progres = mapelIds
            .Select(id => new { Id = id, Al = alokasi.FirstOrDefault(a => a.MataPelajaranId == id)?.JpPerMinggu ?? 0, Tp = terpakai.GetValueOrDefault(id, 0) })
            .Where(x => !(x.Al == 0 && x.Tp == 0))
            .Select(x => new ProgresMapelRow { MataPelajaranId = x.Id, Nama = mapelMaster.TryGetValue(x.Id, out var m) ? m.Nama : "?", Alokasi = x.Al, Terpakai = x.Tp })
            .OrderBy(x => x.Nama)
            .ToList();

        vm.MapelList = (await db.MataPelajaran.ToListAsync())
            .OrderBy(m => m.Urutan == 0).ThenBy(m => m.Urutan).ThenBy(m => m.Nama)
            .Select(m => new MapelKodeOpt { MataPelajaranId = m.MataPelajaranId, Kode = m.Kode, Nama = m.Nama }).ToList();

        vm.GuruList = await GetPengajarAktifAsync();

        var piket = await db.JadwalPiket.Where(p => p.TahunAjaranId == taId && p.Semester.ToString() == sem).ToListAsync();
        vm.PiketPerHari = HariUrut.ToDictionary(h => h, h => piket.Where(p => p.Hari.ToString() == h).Select(p => p.GuruId).ToList());

        return View(vm);
    }

    private async Task<List<GuruOpt>> GetPengajarAktifAsync() =>
        (await db.Guru.Where(g => g.StatusAktif && g.Jabatan != JabatanGuru.karyawan).ToListAsync())
        .OrderBy(g => g.NomorUrut == null).ThenBy(g => g.NomorUrut).ThenBy(g => g.Nama)
        .Select(g => new GuruOpt { GuruId = g.GuruId, NomorUrut = g.NomorUrut, Nama = g.Nama })
        .ToList();

    private static readonly string[] HariUrut = ["senin", "selasa", "rabu", "kamis", "jumat", "sabtu", "minggu"];

    // -------------------------------------------------------------- bacaKode()

    private static readonly Regex KodeRegex = new(@"^(\d*)([A-Z]+)$", RegexOptions.Compiled);

    // Return (guruId, mapelId, error). Kosong -> (null,null,null) = hapus slot.
    private (int? GuruId, int? MapelId, string? Error) BacaKode(string raw, Dictionary<string, int> petaMapel, Dictionary<int, int> petaGuru)
    {
        var kode = Regex.Replace(raw.Trim(), @"\s+", "").ToUpperInvariant();
        if (kode == "") return (null, null, null);

        var m = KodeRegex.Match(kode);
        if (!m.Success) return (null, null, $"kode \"{raw}\" tidak dikenali (contoh benar: 35K)");

        var angka = m.Groups[1].Value;
        var huruf = m.Groups[2].Value;

        if (!petaMapel.TryGetValue(huruf, out var mapelId)) return (null, null, $"kode mata pelajaran \"{huruf}\" belum terdaftar");

        if (angka == "") return (null, mapelId, null);

        var nomor = int.Parse(angka);
        if (!petaGuru.TryGetValue(nomor, out var guruId)) return (null, null, $"guru nomor {nomor} tidak ditemukan/tidak aktif");

        return (guruId, mapelId, null);
    }

    private async Task<(Dictionary<string, int> PetaMapel, Dictionary<int, int> PetaGuru)> BangunPetaAsync()
    {
        var petaMapel = await db.MataPelajaran.Where(m => m.Kode != null).ToDictionaryAsync(m => m.Kode!.ToUpperInvariant(), m => m.MataPelajaranId);
        var petaGuru = (await db.Guru.Where(g => g.StatusAktif && g.NomorUrut != null).ToListAsync()).ToDictionary(g => g.NomorUrut!.Value, g => g.GuruId);
        return (petaMapel, petaGuru);
    }

    // simpanSlot: guruId truthy -> cek bentrok guru dulu. Return null=sukses, else pesan error.
    private async Task<string?> SimpanSlotAsync(int ta, string semester, int kelasId, int jamPelajaranId, int mapelId, int? guruId)
    {
        if (guruId is not null)
        {
            var bentrok = await db.JadwalPelajaran.Include(j => j.Kelas).Include(j => j.MataPelajaran)
                .FirstOrDefaultAsync(j => j.GuruId == guruId && j.JamPelajaranId == jamPelajaranId && j.Semester.ToString() == semester && j.TahunAjaranId == ta && j.KelasId != kelasId);
            if (bentrok is not null)
                return $"Guru tersebut sudah mengajar {bentrok.MataPelajaran.Nama} di kelas {bentrok.Kelas.NamaKelas} pada jam yang sama.";
        }

        var existing = await db.JadwalPelajaran.FirstOrDefaultAsync(j => j.TahunAjaranId == ta && j.Semester.ToString() == semester && j.KelasId == kelasId && j.JamPelajaranId == jamPelajaranId);
        if (existing is not null)
        {
            existing.MataPelajaranId = mapelId;
            existing.GuruId = guruId;
        }
        else
        {
            db.JadwalPelajaran.Add(new Data.Entities.JadwalPelajaran { TahunAjaranId = ta, Semester = Enum.Parse<Semester>(semester), KelasId = kelasId, JamPelajaranId = jamPelajaranId, MataPelajaranId = mapelId, GuruId = guruId });
        }
        await db.SaveChangesAsync();
        return null;
    }

    private async Task HapusSlotAsync(int ta, string semester, int kelasId, int jamPelajaranId)
    {
        var existing = await db.JadwalPelajaran.FirstOrDefaultAsync(j => j.TahunAjaranId == ta && j.Semester.ToString() == semester && j.KelasId == kelasId && j.JamPelajaranId == jamPelajaranId);
        if (existing is not null) { db.JadwalPelajaran.Remove(existing); await db.SaveChangesAsync(); }
    }

    // -------------------------------------------------------------- StoreGrid (simpanGrid)

    [HttpPost("simpan-grid")]
    public async Task<IActionResult> StoreGrid(int tahun_ajaran_id, string? semester, int kelas_id, [FromForm] Dictionary<string, string> sel)
    {
        if (tahun_ajaran_id <= 0 || semester is not ("ganjil" or "genap") || kelas_id <= 0)
        {
            TempData["error"] = "Tahun ajaran / semester / kelas tidak valid.";
            return RedirectToAction(nameof(Index), new { tahun = tahun_ajaran_id, semester, kelas = kelas_id });
        }

        var (petaMapel, petaGuru) = await BangunPetaAsync();
        var jamMaster = await db.JamPelajaran.Where(j => j.TahunAjaranId == tahun_ajaran_id).ToDictionaryAsync(j => j.JamPelajaranId);

        int tersimpan = 0, dihapus = 0;
        var errors = new List<string>();

        foreach (var (jamIdStr, raw) in sel)
        {
            if (!int.TryParse(jamIdStr, out var jamId) || !jamMaster.TryGetValue(jamId, out var jam)) continue;
            var posisi = $"Jam ke-{jam.JamKe} {jam.Hari}";

            var (guruId, mapelId, err) = BacaKode(raw ?? "", petaMapel, petaGuru);
            if (err is not null) { errors.Add($"{posisi}: {err}"); continue; }

            if (mapelId is null)
            {
                await HapusSlotAsync(tahun_ajaran_id, semester, kelas_id, jamId);
                dihapus++;
            }
            else
            {
                var slotErr = await SimpanSlotAsync(tahun_ajaran_id, semester, kelas_id, jamId, mapelId.Value, guruId);
                if (slotErr is not null) errors.Add($"{posisi}: {slotErr}");
                else tersimpan++;
            }
        }

        if (errors.Count > 0) TempData["import_errors"] = JsonSerializer.Serialize(errors);
        TempData["message"] = $"{tersimpan} slot tersimpan, {dihapus} dikosongkan." + (errors.Count > 0 ? " Beberapa sel dilewati - lihat rincian di atas." : "");
        return RedirectToAction(nameof(Index), new { tahun = tahun_ajaran_id, semester, kelas = kelas_id });
    }

    // -------------------------------------------------------------- Salin

    [HttpPost("salin")]
    public async Task<IActionResult> Salin(int tahun_ajaran_id, string? semester, int? sumber_ta, string? sumber_semester)
    {
        IActionResult Balik() => RedirectToAction(nameof(Index), new { tahun = tahun_ajaran_id, semester });

        if (sumber_ta is not > 0 || sumber_semester is not ("ganjil" or "genap"))
        {
            TempData["error"] = "Sumber salinan belum dipilih.";
            return Balik();
        }
        if (sumber_ta == tahun_ajaran_id && sumber_semester == semester)
        {
            TempData["error"] = "Sumber dan tujuan tidak boleh sama.";
            return Balik();
        }

        var sumberRows = await db.JadwalPelajaran.Where(j => j.TahunAjaranId == sumber_ta && j.Semester.ToString() == sumber_semester).ToListAsync();
        var tujuanSet = (await db.JadwalPelajaran.Where(j => j.TahunAjaranId == tahun_ajaran_id && j.Semester.ToString() == semester)
            .Select(j => new { j.KelasId, j.JamPelajaranId }).ToListAsync()).Select(x => (x.KelasId, x.JamPelajaranId)).ToHashSet();

        int disalin = 0, dilewati = 0;

        if (sumber_ta == tahun_ajaran_id)
        {
            // TA sama, semester beda -> jam_pelajaran_id identik langsung.
            foreach (var s in sumberRows)
            {
                if (tujuanSet.Contains((s.KelasId, s.JamPelajaranId))) { dilewati++; continue; }
                db.JadwalPelajaran.Add(new Data.Entities.JadwalPelajaran { TahunAjaranId = tahun_ajaran_id, Semester = Enum.Parse<Semester>(semester!), KelasId = s.KelasId, JamPelajaranId = s.JamPelajaranId, MataPelajaranId = s.MataPelajaranId, GuruId = s.GuruId });
                disalin++;
            }
        }
        else
        {
            // TA beda -> jam_pelajaran_id beda per TA, dipetakan via (hari,jam_ke).
            var jamSumber = await db.JamPelajaran.Where(j => j.TahunAjaranId == sumber_ta).ToDictionaryAsync(j => j.JamPelajaranId);
            var jamTujuanPeta = (await db.JamPelajaran.Where(j => j.TahunAjaranId == tahun_ajaran_id).ToListAsync()).ToDictionary(j => (j.Hari, j.JamKe), j => j.JamPelajaranId);

            foreach (var s in sumberRows)
            {
                if (!jamSumber.TryGetValue(s.JamPelajaranId, out var jamAsal)) { dilewati++; continue; }
                if (!jamTujuanPeta.TryGetValue((jamAsal.Hari, jamAsal.JamKe), out var jamTujuanId)) { dilewati++; continue; }
                if (tujuanSet.Contains((s.KelasId, jamTujuanId))) { dilewati++; continue; }
                db.JadwalPelajaran.Add(new Data.Entities.JadwalPelajaran { TahunAjaranId = tahun_ajaran_id, Semester = Enum.Parse<Semester>(semester!), KelasId = s.KelasId, JamPelajaranId = jamTujuanId, MataPelajaranId = s.MataPelajaranId, GuruId = s.GuruId });
                disalin++;
            }
        }
        await db.SaveChangesAsync();

        TempData["message"] = $"{disalin} slot disalin, {dilewati} dilewati (sudah terisi atau jamnya tidak ada di tahun ajaran ini). Hasil salinan bebas diubah/dihapus.";
        return Balik();
    }

    // -------------------------------------------------------------- Piket (replace-all)

    [HttpPost("simpan-piket")]
    public async Task<IActionResult> SimpanPiket(int tahun_ajaran_id, string? semester, [FromForm(Name = "piket")] Dictionary<string, List<int>> piket)
    {
        IActionResult Balik() => RedirectToAction(nameof(Index), new { tahun = tahun_ajaran_id, semester });
        if (tahun_ajaran_id <= 0 || semester is not ("ganjil" or "genap")) { TempData["error"] = "Tahun ajaran / semester tidak valid."; return Balik(); }

        var lama = await db.JadwalPiket.Where(p => p.TahunAjaranId == tahun_ajaran_id && p.Semester.ToString() == semester).ToListAsync();
        db.JadwalPiket.RemoveRange(lama);

        var guruAktifIds = (await db.Guru.Where(g => g.StatusAktif).Select(g => g.GuruId).ToListAsync()).ToHashSet();
        var n = 0;
        foreach (var (hari, guruIds) in piket)
        {
            if (!HariUrut.Contains(hari)) continue;
            foreach (var gid in guruIds.Distinct())
            {
                if (!guruAktifIds.Contains(gid)) continue;
                db.JadwalPiket.Add(new JadwalPiket { TahunAjaranId = tahun_ajaran_id, Semester = Enum.Parse<Semester>(semester), Hari = Enum.Parse<Hari>(hari), GuruId = gid });
                n++;
            }
        }
        await db.SaveChangesAsync();

        TempData["message"] = $"{n} penugasan piket disimpan.";
        return Balik();
    }

    // -------------------------------------------------------------- Per Guru

    [HttpGet("per-guru")]
    public async Task<IActionResult> PerGuru(int? tahun, string? semester, int? guru)
    {
        var (taId, sem) = await KonteksAsync(tahun, semester);
        var vm = new JadwalPerGuruViewModel
        {
            TahunAjaranId = taId,
            Semester = sem,
            TahunAjaranList = (await db.TahunAjaran.OrderByDescending(t => t.Nama).Select(t => new { t.TahunAjaranId, t.Nama }).ToListAsync())
                .Select(x => (x.TahunAjaranId, x.Nama)).ToList(),
            GuruList = await GetPengajarAktifAsync(),
        };
        if (taId <= 0 || vm.GuruList.Count == 0) return View(vm);

        vm.GuruId = guru is > 0 && vm.GuruList.Any(g => g.GuruId == guru) ? guru : vm.GuruList[0].GuruId;

        var semuaJadwal = await db.JadwalPelajaran.Include(j => j.JamPelajaran).Include(j => j.Kelas).Include(j => j.MataPelajaran)
            .Where(j => j.TahunAjaranId == taId && j.Semester.ToString() == sem)
            .ToListAsync();

        vm.Jadwal = semuaJadwal.Where(j => j.GuruId == vm.GuruId)
            .OrderBy(j => Array.IndexOf(HariUrut, j.JamPelajaran.Hari.ToString())).ThenBy(j => j.JamPelajaran.JamKe)
            .Select(j => new PerGuruJadwalRow { Hari = j.JamPelajaran.Hari.ToString(), JamKe = j.JamPelajaran.JamKe, WaktuLabel = $"{j.JamPelajaran.JamMulai:HH:mm}-{j.JamPelajaran.JamSelesai:HH:mm}", NamaKelas = j.Kelas.NamaKelas, NamaMapel = j.MataPelajaran.Nama })
            .ToList();

        var bebanPerGuru = semuaJadwal.Where(j => j.GuruId is not null).GroupBy(j => j.GuruId!.Value).ToDictionary(g => g.Key, g => g.Count());
        vm.Beban = vm.GuruList.Select(g => new PerGuruBebanRow { GuruId = g.GuruId, NomorUrut = g.NomorUrut, Nama = g.Nama, Jam = bebanPerGuru.GetValueOrDefault(g.GuruId, 0) }).ToList();

        return View(vm);
    }

    // -------------------------------------------------------------- Cetak

    [HttpGet("cetak")]
    public async Task<IActionResult> Cetak(int? tahun, string? semester)
    {
        var (taId, sem) = await KonteksAsync(tahun, semester);
        var ta = await db.TahunAjaran.FindAsync(taId);
        var vm = new JadwalCetakViewModel { TahunAjaranNama = ta?.Nama ?? "-", Semester = sem };
        if (ta is null) return View(vm);

        var tingkatMaster = await TingkatMasterAsync();
        var kelasList = (await db.Kelas.Where(k => k.IsActive).ToListAsync())
            .OrderBy(k => tingkatMaster.TryGetValue(k.Tingkat, out var t) ? t.Urutan : 99).ThenBy(k => k.NamaKelas).ToList();
        vm.KelasList = kelasList.Select(k => new CetakKelasKolom { KelasId = k.KelasId, NamaKelas = k.NamaKelas }).ToList();

        var jamSemua = await db.JamPelajaran.Where(j => j.TahunAjaranId == taId).ToListAsync();
        vm.HariAktif = HariUrut.Where(h => jamSemua.Any(j => j.Hari.ToString() == h)).ToArray();
        var semuaJamKe = jamSemua.Select(j => j.JamKe).Distinct().OrderBy(x => x).ToList();

        var isiSemua = await db.JadwalPelajaran.Include(j => j.MataPelajaran).Include(j => j.Guru)
            .Where(j => j.TahunAjaranId == taId && j.Semester.ToString() == sem)
            .ToListAsync();

        var batasan = await db.JamPelajaranTingkat.ToListAsync();
        var batasanPerJam = batasan.GroupBy(b => b.JamPelajaranId).ToDictionary(g => g.Key, g => g.Select(b => b.TingkatKode).ToHashSet());

        foreach (var h in vm.HariAktif)
        {
            var barisHari = new List<CetakBaris>();
            foreach (var jk in semuaJamKe)
            {
                var jam = jamSemua.FirstOrDefault(j => j.Hari.ToString() == h && j.JamKe == jk);
                if (jam is null) continue;
                var baris = new CetakBaris { JamKe = jk, WaktuLabel = $"{jam.JamMulai:HH:mm}-{jam.JamSelesai:HH:mm}" };
                if (jam.Jenis == JenisJamPelajaran.kegiatan)
                {
                    baris.BarisKegiatan = true;
                    baris.LabelKegiatan = jam.Label;
                }
                else
                {
                    foreach (var k in kelasList)
                    {
                        var dibatasi = batasanPerJam.TryGetValue(jam.JamPelajaranId, out var set) && set.Count > 0 && !set.Contains(k.Tingkat);
                        if (dibatasi) { baris.PerKelas[k.KelasId] = new CetakSel { Kosong = true }; continue; }
                        var isi = isiSemua.FirstOrDefault(j => j.JamPelajaranId == jam.JamPelajaranId && j.KelasId == k.KelasId);
                        if (isi is null) { baris.PerKelas[k.KelasId] = new CetakSel { Kosong = true }; continue; }
                        var kode = (isi.Guru?.NomorUrut is { } no ? no.ToString() : "") + (isi.MataPelajaran.Kode ?? isi.MataPelajaran.Nama);
                        baris.PerKelas[k.KelasId] = new CetakSel { Kosong = false, Kode = kode };
                    }
                }
                barisHari.Add(baris);
            }
            vm.PerHari[h] = barisHari;
        }

        var piket = await db.JadwalPiket.Include(p => p.Guru).Where(p => p.TahunAjaranId == taId && p.Semester.ToString() == sem).ToListAsync();
        vm.PiketPerHari = HariUrut.ToDictionary(h => h, h => piket.Where(p => p.Hari.ToString() == h).Select(p => (h, p.Guru.NomorUrut, p.Guru.Nama)).ToList());

        vm.LegendMapel = (await db.MataPelajaran.Where(m => m.Kode != null).ToListAsync()).OrderBy(m => m.Kode).Select(m => new MapelKodeOpt { MataPelajaranId = m.MataPelajaranId, Kode = m.Kode, Nama = m.Nama }).ToList();
        vm.LegendGuru = (await GetPengajarAktifAsync()).Where(g => g.NomorUrut is not null).OrderBy(g => g.NomorUrut).ToList();

        return View(vm);
    }

    // -------------------------------------------------------------- Import

    [HttpGet("import")]
    public async Task<IActionResult> Import(int? tahun, string? semester)
    {
        var (taId, sem) = await KonteksAsync(tahun, semester);
        ViewBag.TahunAjaranId = taId;
        ViewBag.Semester = sem;
        var ta = await db.TahunAjaran.FindAsync(taId);
        ViewBag.TahunAjaranNama = ta?.Nama ?? "-";
        ViewBag.JumlahKelas = await db.Kelas.CountAsync(k => k.IsActive);
        ViewBag.JumlahJam = await db.JamPelajaran.CountAsync(j => j.TahunAjaranId == taId);
        return View();
    }

    private record Kerangka(string[] HariAktif, List<Kelas> KelasList, List<Data.Entities.JamPelajaran> JamSemua, List<int> SemuaJamKe);

    private async Task<Kerangka> KerangkaGridAsync(int taId)
    {
        var jamSemua = await db.JamPelajaran.Where(j => j.TahunAjaranId == taId).ToListAsync();
        var hariAktif = HariUrut.Where(h => jamSemua.Any(j => j.Hari.ToString() == h)).ToArray();
        var tingkatMaster = await TingkatMasterAsync();
        var kelasList = (await db.Kelas.Where(k => k.IsActive).ToListAsync())
            .OrderBy(k => tingkatMaster.TryGetValue(k.Tingkat, out var t) ? t.Urutan : 99).ThenBy(k => k.NamaKelas).ToList();
        var semuaJamKe = jamSemua.Select(j => j.JamKe).Distinct().OrderBy(x => x).ToList();
        return new Kerangka(hariAktif, kelasList, jamSemua, semuaJamKe);
    }

    [HttpGet("template")]
    public async Task<IActionResult> DownloadTemplate(int? tahun, string? semester)
    {
        var (taId, sem) = await KonteksAsync(tahun, semester);
        if (taId <= 0) { TempData["error"] = "Tahun ajaran belum dipilih."; return RedirectToAction(nameof(Import)); }

        var k = await KerangkaGridAsync(taId);
        if (k.HariAktif.Length == 0 || k.KelasList.Count == 0)
        {
            TempData["error"] = "Isi dulu Jam Belajar (menu Kurikulum) dan data Kelas sebelum membuat template.";
            return RedirectToAction(nameof(Import), new { tahun = taId, semester = sem });
        }

        var ta = await db.TahunAjaran.FindAsync(taId);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Jadwal Pelajaran");
        ws.Cell(1, 1).Value = $"JADWAL PELAJARAN {ta?.Nama} - Semester {(sem == "ganjil" ? "Ganjil" : "Genap")}";

        // Baris 2 = hari (diulang per kolom kelas, TIDAK di-merge), baris 3 = nama kelas.
        var col = 3; // col1=Jam ke, col2=Waktu, kolom kelas mulai col3
        foreach (var h in k.HariAktif)
        {
            foreach (var kelas in k.KelasList)
            {
                ws.Cell(2, col).Value = h;
                ws.Cell(3, col).Value = kelas.NamaKelas;
                col++;
            }
        }
        ws.Cell(3, 1).Value = "Jam ke";
        ws.Cell(3, 2).Value = "Waktu";

        var row = 4;
        foreach (var jk in k.SemuaJamKe)
        {
            ws.Cell(row, 1).Value = jk;
            // Ambil jam pertama menurut URUTAN HARI (senin dulu), bukan urutan sembarang
            // dari DB - supaya label Waktu konsisten dgn yang ditampilkan grid Index.
            var contohJam = k.HariAktif
                .Select(h => k.JamSemua.FirstOrDefault(j => j.Hari.ToString() == h && j.JamKe == jk))
                .FirstOrDefault(j => j is not null);
            ws.Cell(row, 2).Value = contohJam is not null ? $"{contohJam.JamMulai:HH:mm}-{contohJam.JamSelesai:HH:mm}" : "";

            col = 3;
            foreach (var h in k.HariAktif)
            {
                foreach (var kelas in k.KelasList)
                {
                    var jam = k.JamSemua.FirstOrDefault(j => j.Hari.ToString() == h && j.JamKe == jk);
                    ws.Cell(row, col).Value = jam is null ? "" : (jam.Jenis == JenisJamPelajaran.kegiatan ? (jam.Label ?? "Kegiatan") : "");
                    col++;
                }
            }
            row++;
        }

        var mapelSheet = wb.Worksheets.Add("Kode Mapel");
        mapelSheet.Cell(1, 1).Value = "Kode"; mapelSheet.Cell(1, 2).Value = "Mata Pelajaran";
        var mapelR = 2;
        foreach (var m in await db.MataPelajaran.Where(m => m.Kode != null).OrderBy(m => m.Kode).ToListAsync())
        {
            mapelSheet.Cell(mapelR, 1).Value = m.Kode; mapelSheet.Cell(mapelR, 2).Value = m.Nama; mapelR++;
        }

        var guruSheet = wb.Worksheets.Add("Kode Guru");
        guruSheet.Cell(1, 1).Value = "Nomor"; guruSheet.Cell(1, 2).Value = "Nama"; guruSheet.Cell(1, 3).Value = "Jabatan";
        var guruR = 2;
        foreach (var g in (await GetPengajarAktifAsync()).Where(g => g.NomorUrut is not null).OrderBy(g => g.NomorUrut))
        {
            guruSheet.Cell(guruR, 1).Value = g.NomorUrut; guruSheet.Cell(guruR, 2).Value = g.Nama; guruR++;
        }

        var panduan = wb.Worksheets.Add("Panduan");
        panduan.Cell(1, 1).Value = @"CARA MENGISI

1. Isi tiap sel dengan kode: <nomor guru><kode mapel>. Contoh: 35K = guru nomor 35 mengajar mapel K.
2. Kalau mata pelajaran itu tidak punya guru tetap per kelas (mis. BTAQ berkelompok), tulis kode mapelnya saja. Contoh: F
3. Kosongkan sel yang memang tidak ada pelajaran.
4. Sel yang sudah berisi nama kegiatan (Istirahat, Ishoma, dll) JANGAN diubah - itu diatur di menu Kurikulum & Jam Belajar.
5. Daftar kode ada di sheet ""Kode Mapel"" dan ""Kode Guru"".
6. Jangan mengubah baris judul, baris HARI, baris nama kelas, dan kolom ""Jam ke""/""Waktu"".

Baris yang bentrok jam gurunya akan dilewati dan dilaporkan - jadwal lama tidak akan tertimpa diam-diam.";

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "template_jadwal_pelajaran.xlsx");
    }

    private static string Normalize(string v) => Regex.Replace(v.Trim(), @"\s+", "").ToUpperInvariant();

    [HttpPost("process-import")]
    public async Task<IActionResult> ProcessImport(IFormFile? file, int tahun_ajaran_id, string? semester)
    {
        IActionResult GagalRedirect(string pesan)
        {
            TempData["error"] = pesan;
            return RedirectToAction(nameof(Import), new { tahun = tahun_ajaran_id, semester });
        }

        if (tahun_ajaran_id <= 0 || semester is not ("ganjil" or "genap")) return GagalRedirect("Tahun ajaran / semester tidak valid.");
        if (file is null || file.Length == 0) return GagalRedirect("File tidak ditemukan.");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls") return GagalRedirect("Format file harus .xlsx atau .xls.");
        if (file.Length > 5 * 1024 * 1024) return GagalRedirect("Ukuran file terlalu besar. Maksimal 5 MB.");

        List<string[]> rows;
        try
        {
            using var stream = file.OpenReadStream();
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();
            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 1;
            rows = ws.RowsUsed().Select(r => Enumerable.Range(1, lastCol).Select(i => r.Cell(i).GetString().Trim()).ToArray()).ToList();
        }
        catch (Exception ex)
        {
            return GagalRedirect($"Gagal membaca file Excel: {ex.Message}");
        }

        if (rows.Count < 4) return GagalRedirect("File tidak berisi data jadwal. Pakai template yang diunduh dari halaman ini.");

        var k = await KerangkaGridAsync(tahun_ajaran_id);
        var barisHari = rows[1];
        var barisKelas = rows[2];
        var kelasByNorm = k.KelasList.ToDictionary(kl => Normalize(kl.NamaKelas), kl => kl);

        var kolomTakDikenal = new List<string>();
        var kolom = new Dictionary<int, (string Hari, Kelas Kelas)>(); // colIndex (0-based) -> resolved

        var lastColCount = Math.Max(barisHari.Length, barisKelas.Length);
        for (var c = 2; c < lastColCount; c++)
        {
            var h = c < barisHari.Length ? barisHari[c].Trim().ToLowerInvariant() : "";
            var nk = c < barisKelas.Length ? barisKelas[c].Trim() : "";
            if (h == "" && nk == "") continue;

            if (!HariUrut.Contains(h))
            {
                kolomTakDikenal.Add($"hari \"{h}\" tidak dikenali");
                continue;
            }
            var norm = Normalize(nk);
            if (!kelasByNorm.TryGetValue(norm, out var kelasEntity))
            {
                kolomTakDikenal.Add($"kelas \"{nk}\" (kolom hari {h}) tidak ada di data kelas aktif");
                continue;
            }
            kolom[c] = (h, kelasEntity);
        }

        if (kolom.Count == 0)
            return GagalRedirect("Tidak ada kolom kelas yang dikenali. Pastikan baris HARI dan baris nama kelas tidak diubah, dan unduh ulang template kalau data kelas berubah.");

        var (petaMapel, petaGuru) = await BangunPetaAsync();
        int tersimpan = 0, dilewati = 0;
        var errors = new List<string>();

        for (var r = 3; r < rows.Count; r++)
        {
            var cells = rows[r];
            if (cells.Length == 0) continue;
            if (!int.TryParse(cells[0], out var jamKe) || jamKe <= 0) continue;

            foreach (var (colIdx, info) in kolom)
            {
                if (colIdx >= cells.Length) continue;
                var raw = cells[colIdx];
                if (raw == "") continue;

                var jam = k.JamSemua.FirstOrDefault(j => j.Hari.ToString() == info.Hari && j.JamKe == jamKe);
                if (jam is null) continue; // jam tak ada di hari itu -> skip diam-diam
                if (jam.Jenis == JenisJamPelajaran.kegiatan) continue; // diatur di menu Kurikulum

                var posisi = $"{info.Hari} jam ke-{jamKe}, kelas {info.Kelas.NamaKelas}";
                var (guruId, mapelId, err) = BacaKode(raw, petaMapel, petaGuru);
                if (err is not null) { errors.Add($"{posisi}: {err}"); dilewati++; continue; }
                if (mapelId is null) continue; // sel kosong (setelah normalisasi) -> skip diam-diam

                var slotErr = await SimpanSlotAsync(tahun_ajaran_id, semester, info.Kelas.KelasId, jam.JamPelajaranId, mapelId.Value, guruId);
                if (slotErr is not null) { errors.Add($"{posisi}: {slotErr}"); dilewati++; }
                else tersimpan++;
            }
        }

        if (kolomTakDikenal.Count > 0)
            errors.Insert(0, "KOLOM DILEWATI (seluruh isinya tidak ikut masuk): " + string.Join("; ", kolomTakDikenal));

        if (errors.Count > 0) TempData["import_errors"] = JsonSerializer.Serialize(errors);
        var msg = $"Import selesai: {tersimpan} slot tersimpan, {dilewati} dilewati.";
        if (kolomTakDikenal.Count > 0) msg += $" PERHATIAN: {kolomTakDikenal.Count} kolom tidak dikenali dan seluruh isinya dilewati - lihat rincian di atas.";
        TempData["message"] = msg;
        return RedirectToAction(nameof(Index), new { tahun = tahun_ajaran_id, semester });
    }
}
