using System.Text.RegularExpressions;
using DataMaster.Data;
using DataMaster.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Web.Services;

public record PsbResult(bool Success, string Message, int? SiswaId = null);

// Port 1:1 dari app/Libraries/PsbProcessor.php - dipakai controller CalonSiswa
// (jalur web) DAN nantinya service sinkronisasi Hub API (jalur pullKeputusanPsb,
// saat keputusan Terima/Tolak dititipkan dari Mobile-app) - SAMA PERSIS logic-nya
// di kedua jalur, lihat 01-siswa-psb.md §3.23-3.26.
public class PsbService(DataMasterDbContext db)
{
    public async Task<PsbResult> TerimaAsync(int calonSiswaId, string? nisInput, int kelasId)
    {
        var calon = await db.CalonSiswa.FindAsync(calonSiswaId);
        if (calon is null) return new PsbResult(false, $"Calon siswa dengan ID {calonSiswaId} tidak ditemukan.");
        if (calon.Status != StatusCalonSiswa.menunggu) return new PsbResult(false, "Calon siswa ini sudah diproses sebelumnya.");

        string nis;
        var trimmed = (nisInput ?? "").Trim();
        if (trimmed == "")
        {
            nis = await GenerateNisAsync();
        }
        else
        {
            if (trimmed.Length > 20 || !Regex.IsMatch(trimmed, @"^\d+$"))
            {
                return new PsbResult(false, "NIS wajib angka, maksimal 20 digit.");
            }
            nis = trimmed;
        }

        if (await db.Siswa.AnyAsync(s => s.Nis == nis))
        {
            return new PsbResult(false, $"NIS {nis} sudah dipakai siswa lain.");
        }

        var kelas = await db.Kelas.FindAsync(kelasId);
        if (kelas is null) return new PsbResult(false, "Kelas tidak ditemukan.");

        var siswaBaru = new Siswa
        {
            Nama = calon.Nama,
            JenisKelamin = calon.JenisKelamin,
            Nis = nis,
            Nisn = calon.Nisn,
            KelasId = kelasId,
            Status = StatusSiswa.aktif,
            TempatLahir = calon.TempatLahir,
            TanggalLahir = calon.TanggalLahir,
            AsalSekolah = calon.AsalSekolah,
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
            // Nama file dokumen disalin APA ADANYA (bukan salin fisik file) - baris
            // siswa baru menunjuk ke file yang sama dgn calon_siswa, lihat §1.4.
            DokumenKk = calon.DokumenKk,
            DokumenAkta = calon.DokumenAkta,
            DokumenKia = calon.DokumenKia,
            DokumenIjazah = calon.DokumenIjazah,
        };
        db.Siswa.Add(siswaBaru);
        await db.SaveChangesAsync();

        calon.Status = StatusCalonSiswa.diterima;
        calon.SiswaId = siswaBaru.SiswaId;
        await db.SaveChangesAsync();

        return new PsbResult(true, $"'{calon.Nama}' berhasil diterima sebagai siswa aktif.", siswaBaru.SiswaId);
    }

    public async Task<PsbResult> TolakAsync(int calonSiswaId, string? catatan)
    {
        var calon = await db.CalonSiswa.FindAsync(calonSiswaId);
        if (calon is null) return new PsbResult(false, $"Calon siswa dengan ID {calonSiswaId} tidak ditemukan.");
        if (calon.Status != StatusCalonSiswa.menunggu) return new PsbResult(false, "Calon siswa ini sudah diproses sebelumnya.");

        var trimmed = catatan?.Trim();
        calon.Status = StatusCalonSiswa.ditolak;
        calon.Catatan = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        await db.SaveChangesAsync();

        return new PsbResult(true, $"'{calon.Nama}' ditandai tidak diterima.");
    }

    // Format PERSIS: prefix 4-digit dari tahun ajaran aktif (2 digit akhir tahun1 +
    // 2 digit akhir tahun2, mis. "2026/2027"->"2627"; tidak ada/format salah->"0000")
    // + 4 digit urut lanjutan (BUKAN COUNT - supaya tidak menabrak nomor bekas kalau
    // ada baris dihapus/NIS lama tak berurutan sempurna). Lihat §3.24.
    private async Task<string> GenerateNisAsync()
    {
        var ta = await db.TahunAjaran.FirstOrDefaultAsync(t => t.IsActive);
        var prefix = "0000";
        if (ta is not null)
        {
            var m = Regex.Match(ta.Nama, @"^(\d{4})/(\d{4})$");
            if (m.Success)
            {
                prefix = m.Groups[1].Value[2..] + m.Groups[2].Value[2..];
            }
        }

        var existingTerbaru = await db.Siswa
            .Where(s => s.Nis.StartsWith(prefix))
            .OrderByDescending(s => s.Nis)
            .Select(s => s.Nis)
            .FirstOrDefaultAsync();

        var urut = 1;
        if (existingTerbaru is not null && existingTerbaru.Length == prefix.Length + 4
            && int.TryParse(existingTerbaru[prefix.Length..], out var lastUrut))
        {
            urut = lastUrut + 1;
        }

        string nis;
        do
        {
            nis = prefix + urut.ToString().PadLeft(4, '0');
            urut++;
        }
        while (await db.Siswa.AnyAsync(s => s.Nis == nis));

        return nis;
    }
}
