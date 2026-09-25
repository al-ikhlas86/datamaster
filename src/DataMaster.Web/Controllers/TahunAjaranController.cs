using Microsoft.AspNetCore.Authorization;
using System.Text.RegularExpressions;
using DataMaster.Data;
using DataMaster.Data.Entities;
using DataMaster.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Controllers;

// Port 1:1 dari app/Controllers/TahunAjaran.php - lihat 02-guru-kelas-struktur.md §9.
[Authorize(Roles = "admin")]
[Route("tahun-ajaran")]
public class TahunAjaranController(DataMasterDbContext db, TahunAjaranKerjaService tahunAjaranKerja, KenaikanKelasService kenaikanKelas) : Controller
{
    // Ganti Tahun Ajaran Kerja (2026-09-25) - TIDAK menyentuh IsActive sama sekali,
    // cuma preferensi GLOBAL "sedang kerja/persiapan di tahun mana" (lihat
    // TahunAjaranKerjaService). Dipanggil dari selector di sidebar, semua mode
    // instalasi Pendidikan - redirect balik ke halaman asal (Referer) supaya TU
    // gak kehilangan posisi.
    [HttpPost("set-kerja")]
    public async Task<IActionResult> SetKerja(int tahun_ajaran_id)
    {
        if (await db.TahunAjaran.AnyAsync(t => t.TahunAjaranId == tahun_ajaran_id))
        {
            await tahunAjaranKerja.SetKerjaIdAsync(tahun_ajaran_id);
        }
        return Redirect(Request.Headers.Referer.ToString() is { Length: > 0 } r ? r : Url.Action(nameof(Index))!);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var list = await db.TahunAjaran.OrderByDescending(t => t.Nama).ToListAsync();
        return View(list);
    }

    [HttpPost("store")]
    public async Task<IActionResult> Store(string? nama)
    {
        var n = (nama ?? "").Trim();
        if (n == "" || !Regex.IsMatch(n, @"^\d{4}/\d{4}$"))
        {
            TempData["error"] = "Format tahun ajaran tidak valid (harus berupa angka dengan format YYYY/YYYY).";
            return RedirectToAction(nameof(Index));
        }
        if (await db.TahunAjaran.AnyAsync(t => t.Nama == n))
        {
            // PHP asli TIDAK menyediakan pesan custom utk aturan is_unique ini (hanya
            // regex_match yang dikustomisasi) - jatuh ke pesan default CI4 apa adanya,
            // sengaja dipertahankan meski jadi satu-satunya pesan berbahasa Inggris di
            // aplikasi ini. Lihat 02-guru-kelas-struktur.md §9.2.
            TempData["error"] = "The nama field must contain a unique value.";
            return RedirectToAction(nameof(Index));
        }

        // Tahun ajaran BARU SELALU dibuat NON-aktif - harus diaktifkan manual terpisah.
        db.TahunAjaran.Add(new TahunAjaran { Nama = n, IsActive = false });
        await db.SaveChangesAsync();

        TempData["message"] = "Tahun ajaran berhasil ditambahkan.";
        return RedirectToAction(nameof(Index));
    }

    // confirm=false (default, klik pertama dari tombol Aktifkan): kalau ada siswa
    // aktif yang belum direncanakan (RencanaKenaikan) utk tahun ini, BERHENTI dulu &
    // minta konfirmasi eksplisit - dari sini TU bisa balik isi rencananya dulu, atau
    // klik "Lanjutkan tanpa mereka" (submit ulang dgn confirm=true) kalau memang
    // sengaja. Mencegah siswa "kelupaan" ter-skip tanpa disadari saat aktivasi.
    [HttpPost("{id:int}/set-active")]
    public async Task<IActionResult> SetActive(int id, bool confirm = false)
    {
        var ta = await db.TahunAjaran.FindAsync(id);
        if (ta is null)
        {
            TempData["error"] = "Tahun ajaran tidak ditemukan.";
            return RedirectToAction(nameof(Index));
        }

        var taLama = await db.TahunAjaran.FirstOrDefaultAsync(t => t.IsActive);
        var rencanaList = await db.RencanaKenaikan.Where(r => r.TahunAjaranTujuanId == id).ToListAsync();

        if (!confirm)
        {
            var rencanaSiswaIds = rencanaList.Select(r => r.SiswaId).ToHashSet();
            var siswaAktifIds = await db.Siswa.Where(s => s.Status == Data.StatusSiswa.aktif).Select(s => s.SiswaId).ToListAsync();
            var belumDirencanakan = siswaAktifIds.Count(sid => !rencanaSiswaIds.Contains(sid));
            if (belumDirencanakan > 0)
            {
                TempData["warning"] = $"Masih ada {belumDirencanakan} siswa aktif yang belum direncanakan (naik kelas/lulus) untuk {ta.Nama} - mereka TIDAK akan pindah kelas kalau tetap lanjut aktifkan sekarang.";
                TempData["confirm_set_active_id"] = id;
                return RedirectToAction(nameof(Index));
            }
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Terapkan SEMUA rencana kenaikan/kelulusan DULU, SELAGI taLama masih
            // IsActive=true - urutan ini KRUSIAL supaya RiwayatAkademik tercatat di
            // tahun ajaran SUMBER yang benar (tahun siswa itu beneran belajar), bukan
            // tahun tujuan yang baru saja diaktifkan (lihat KenaikanKelasService).
            if (rencanaList.Count > 0 && taLama is not null)
            {
                var keputusan = rencanaList.Select(r => (r.SiswaId, r.KelasTujuanId, r.Lulus)).ToList();
                await kenaikanKelas.TerapkanAsync(taLama.TahunAjaranId, keputusan);
                db.RencanaKenaikan.RemoveRange(rencanaList);
            }

            // Nonaktifkan SEMUA baris dulu (tanpa where), baru aktifkan yang dipilih -
            // SENGAJA tidak memicu sinkronisasi jabatan guru sama sekali (lihat
            // 02-guru-kelas-struktur.md §12 poin 11 - perilaku APA ADANYA dari PHP asli).
            await db.TahunAjaran.ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, false));
            ta.IsActive = true;
            // TAK ikut "pulang" ke tahun yang baru aktif - semua TU otomatis balik ke
            // tampilan normal, gak nyangkut di tahun yang barusan selesai diproses.
            await tahunAjaranKerja.ResetKeAktifAsync(id);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            TempData["error"] = "Terjadi kesalahan saat mengaktifkan tahun ajaran - tidak ada yang berubah.";
            return RedirectToAction(nameof(Index));
        }

        TempData["message"] = rencanaList.Count > 0
            ? $"Tahun ajaran {ta.Nama} berhasil diaktifkan - {rencanaList.Count} rencana kenaikan/kelulusan diterapkan."
            : $"Tahun ajaran {ta.Nama} berhasil diaktifkan.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var ta = await db.TahunAjaran.FindAsync(id);
        if (ta is null)
        {
            TempData["error"] = "Tahun ajaran tidak ditemukan.";
            return RedirectToAction(nameof(Index));
        }
        if (ta.IsActive)
        {
            TempData["error"] = "Tidak dapat menghapus tahun ajaran yang sedang aktif.";
            return RedirectToAction(nameof(Index));
        }

        // SENGAJA SELALU gagal, bahkan untuk tahun tidak aktif - tidak ada baris kode
        // yang benar-benar menghapus. Ini APA ADANYA dari PHP asli (arsip historis
        // permanen), BUKAN bug untuk "diperbaiki" - lihat §12 poin 12.
        TempData["error"] = $"Tahun ajaran {ta.Nama} dipertahankan sebagai arsip historis dan tidak dapat dihapus.";
        return RedirectToAction(nameof(Index));
    }
}
