using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DataMaster.Data;
using DataMaster.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DataMaster.Web.Services;

// Port 1:1 dari app/Commands/SyncPush.php - lihat 04-infra-auth-sync.md §7. Push
// SATU ARAH ke Hub API (kecuali pullKeputusanPsb/laporKeputusanSelesai, satu2nya
// jalur dua arah). unit_id TIDAK PERNAH dikirim - Hub API menurunkannya SENDIRI
// dari token (Authorization: Bearer {token}). source_id di semua kolom *_source_id
// SELALU id lokal ASLI (kelas_id/guru_id/dst di SQLite ini), BUKAN id hasil sync
// Hub API - lihat §7.7.
public class HubApiSyncService(DataMasterDbContext db, HttpClient http, IOptions<AppOptions> options, ILogger<HubApiSyncService> logger, PsbService psb)
{
    // Fingerprint HARUS menyertakan versi protokol - kalau BENTUK payload berubah,
    // versi ini WAJIB dinaikkan juga (lihat §7.4). "v4-full" dipertahankan SAMA
    // PERSIS dgn versi payload PHP asli (guru sudah termasuk is_kepala_sekolah +
    // status_keluar) - supaya PC yang datanya kebetulan identik dgn kiriman PHP
    // lama tidak perlu mengirim ulang semua begitu berpindah ke implementasi ini.
    private const string ProtocolVersion = "v4-full";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task RunAsync(CancellationToken ct = default)
    {
        var url = (options.Value.HubApiUrl ?? "").Trim().TrimEnd('/');
        var token = (options.Value.HubApiToken ?? "").Trim();
        if (url == "" || token == "")
        {
            logger.LogInformation("Sync dilewati: hub_api.url/token belum diisi.");
            return;
        }

        var installType = (options.Value.InstallType ?? "pendidikan").Trim().ToLowerInvariant();

        if (installType != "perusahaan")
        {
            await PushKelasAsync(url, token, ct);
            await PushTahunAjaranAsync(url, token, ct);
            await PushSiswaAsync(url, token, ct);
            await PushMataPelajaranAsync(url, token, ct);
            await PushJamPelajaranAsync(url, token, ct);
        }

        await PushGuruAsync(url, token, companyOnly: installType == "perusahaan", ct);

        if (installType != "perusahaan")
        {
            // Jadwal Pelajaran, Wali Kelas, Kepala Sekolah BUTUH guru & kelas
            // SUDAH tersinkron dulu - makanya ditaruh PALING TERAKHIR (§7.2).
            await PushWaliKelasAsync(url, token, ct);
            await PushKepalaSekolahAsync(url, token, ct);
            await PushJadwalPelajaranAsync(url, token, ct);
            await PushKalenderAkademikAsync(url, token, ct);
            await PushCalonSiswaAsync(url, token, ct);
            await PullKeputusanPsbAsync(url, token, ct);
        }
    }

    // ---------------------------------------------------------------- Helpers

    private async Task<Dictionary<string, string>> LabelMapTingkatAsync() => await db.Tingkat.ToDictionaryAsync(t => t.Kode, t => t.Nama);

    private static string LabelFallbackTingkat(string kode) => string.IsNullOrEmpty(kode) ? "Semua Tingkat" : (int.TryParse(kode, out _) ? $"Kelas {kode}" : kode);

    private record PostResult(bool Sukses, int Synced, int Changed, int Unchanged, int Deleted, int ErrorCount);

    // Port method post() private PHP asli - fingerprint hemat-jaringan (§7.4),
    // perilaku tabel-kosong-vs-snapshot (§7.5), format request/response (§7.6).
    private async Task<PostResult?> PostAsync(string baseUrl, string token, string path, List<object> rows, bool full, string label, CancellationToken ct)
    {
        if (rows.Count == 0 && !full)
        {
            logger.LogInformation("Sync {Label}: (tidak ada data)", label);
            return null;
        }

        var json = JsonSerializer.Serialize(rows, JsonOpts);
        var fingerprintBaru = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{ProtocolVersion}|{json}"))).ToLowerInvariant();
        var fpFile = FingerprintFilePath(path);

        if (File.Exists(fpFile) && (await File.ReadAllTextAsync(fpFile, ct)).Trim() == fingerprintBaru)
        {
            logger.LogInformation("Sync {Label}: (tidak berubah sejak sync terakhir - tidak dikirim)", label);
            return null;
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}{path}");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            req.Content = JsonContent.Create(new { data = rows, full }, options: JsonOpts);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));
            using var resp = await http.SendAsync(req, cts.Token);

            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Sync {Label}: GAGAL HTTP {Status} - {Body}", label, (int)resp.StatusCode, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("success", out var successEl) || !successEl.GetBoolean())
            {
                var msg = root.TryGetProperty("message", out var m) ? m.GetString() : body;
                logger.LogWarning("Sync {Label}: DITOLAK server - {Message}", label, msg);
                return null;
            }

            var synced = GetIntFlexible(root, "synced") ?? 0;
            var changed = GetIntFlexible(root, "changed") ?? synced;
            var unchanged = GetIntFlexible(root, "unchanged") ?? 0;
            var deleted = GetIntFlexible(root, "deleted") ?? 0;
            var errorCount = root.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Array ? e.GetArrayLength() : 0;

            var warna = errorCount > 0 ? "PERHATIAN" : "OK";
            logger.LogInformation("Sync {Label}: {Synced} diterima; {Changed} berubah, {Unchanged} tetap, {Deleted} dihapus; {Errors} error. [{Warna}]",
                label, synced, changed, unchanged, deleted, errorCount, warna);

            // Fingerprint BARU disimpan HANYA setelah SEMUA baris diterima sukses
            // (errorCount===0) - lihat §7.4/§13 poin 13.
            if (errorCount == 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fpFile)!);
                await File.WriteAllTextAsync(fpFile, fingerprintBaru, ct);
            }

            return new PostResult(true, synced, changed, unchanged, deleted, errorCount);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sync {Label}: GAGAL koneksi - {Message}", label, ex.Message);
            return null;
        }
    }

    // Hub API (CI4 + MySQLi) mengembalikan kolom hasil query mentah SEBAGAI STRING
    // di JSON (mis. baris PsbKeputusanModel "id":"3"), TAPI angka yang dihitung PHP
    // sendiri (counter $synced++ dst di SyncController) tetap JSON number asli -
    // dua sumber berbeda, dua bentuk berbeda. Helper ini menerima KEDUANYA supaya
    // tidak rapuh terhadap perbedaan itu (ditemukan nyata saat uji end-to-end
    // pullKeputusanPsb - lihat catatan modul di 00-INDEX.md).
    private static int? GetIntFlexible(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v) || v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (v.ValueKind == JsonValueKind.Number) return v.GetInt32();
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var n)) return n;
        return null;
    }

    private string FingerprintFilePath(string path)
    {
        var dbPath = SyncStateDir();
        var namaFile = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(path))).ToLowerInvariant() + ".hash";
        return Path.Combine(dbPath, namaFile);
    }

    // sync_state/ SENGAJA di luar database (bukan tabel) - supaya TIDAK ikut
    // ter-backup/ter-restore (§7.4): kalau hilang, efeknya cuma 1 siklus penuh.
    private string SyncStateDir()
    {
        var cs = db.Database.GetConnectionString() ?? "Data Source=App_Data/datamaster.db";
        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(cs);
        var dbDir = Path.GetDirectoryName(Path.GetFullPath(builder.DataSource))!;
        return Path.Combine(dbDir, "..", "sync_state");
    }

    // ---------------------------------------------------------------- Push per entitas

    private async Task PushKelasAsync(string url, string token, CancellationToken ct)
    {
        var labelMap = await LabelMapTingkatAsync();
        var rows = (await db.Kelas.ToListAsync(ct)).Select(k => (object)new
        {
            SourceId = k.KelasId,
            Tingkat = labelMap.TryGetValue(k.Tingkat, out var nama) ? nama : LabelFallbackTingkat(k.Tingkat),
            TingkatKode = k.Tingkat,
            Nama = k.NamaKelas,
            Kelompok = k.Kelompok,
            IsActive = k.IsActive ? 1 : 0,
        }).ToList();
        await PostAsync(url, token, "/api/v1/sync/kelas", rows, full: false, "Kelas", ct);
    }

    private async Task PushTahunAjaranAsync(string url, string token, CancellationToken ct)
    {
        var rows = (await db.TahunAjaran.ToListAsync(ct)).Select(t => (object)new
        {
            SourceId = t.TahunAjaranId,
            t.Nama,
            IsActive = t.IsActive ? 1 : 0,
        }).ToList();
        await PostAsync(url, token, "/api/v1/sync/tahun-ajaran", rows, full: false, "Tahun Ajaran", ct);
    }

    private async Task PushSiswaAsync(string url, string token, CancellationToken ct)
    {
        var rows = (await db.Siswa.ToListAsync(ct)).Select(s => (object)new
        {
            SourceId = s.SiswaId,
            s.Nama,
            s.Nis,
            s.Nisn,
            JenisKelamin = s.JenisKelamin.ToString(),
            KelasSourceId = s.KelasId,
            HpOrtu = s.NoHandphone,
            Status = s.Status.ToString(),
        }).ToList();
        await PostAsync(url, token, "/api/v1/sync/siswa", rows, full: false, "Siswa", ct);
    }

    private async Task PushMataPelajaranAsync(string url, string token, CancellationToken ct)
    {
        var rows = (await db.MataPelajaran.ToListAsync(ct)).Select(m => (object)new
        {
            SourceId = m.MataPelajaranId,
            m.Nama,
            m.Kode,
            m.Kelompok,
        }).ToList();
        await PostAsync(url, token, "/api/v1/sync/mata-pelajaran", rows, full: true, "Mata Pelajaran", ct);
    }

    private async Task PushJamPelajaranAsync(string url, string token, CancellationToken ct)
    {
        var rows = (await db.JamPelajaran.ToListAsync(ct)).Select(j => (object)new
        {
            SourceId = j.JamPelajaranId,
            TahunAjaranSourceId = j.TahunAjaranId,
            Hari = j.Hari.ToString(),
            j.JamKe,
            JamMulai = j.JamMulai.ToString("HH:mm"),
            JamSelesai = j.JamSelesai.ToString("HH:mm"),
            Jenis = j.Jenis.ToString(),
            j.Label,
        }).ToList();
        await PostAsync(url, token, "/api/v1/sync/jam-pelajaran", rows, full: true, "Jam Pelajaran", ct);
    }

    private async Task PushGuruAsync(string url, string token, bool companyOnly, CancellationToken ct)
    {
        var query = db.Guru.AsQueryable();
        if (companyOnly) query = query.Where(g => g.Jabatan == JabatanGuru.karyawan);
        var guruList = await query.ToListAsync(ct);

        // kelas_source_id TIDAK LAGI dari kolom guru.kelas_id (dibuang) - di-lookup
        // dari WaliKelas. ADAPTASI: skema di sini menyimpan riwayat wali per TAHUN
        // AJARAN (bukan 1 baris per guru spt asumsi lama PHP) - dipetakan ke wali
        // kelas TAHUN AJARAN AKTIF (kalau ada), representasi paling relevan utk
        // konsumen "kelas WALI SAAT INI" - riwayat lengkap tetap terkirim utuh
        // lewat pushWaliKelas (full snapshot terpisah).
        var taAktifId = await db.TahunAjaran.Where(t => t.IsActive).Select(t => t.TahunAjaranId).FirstOrDefaultAsync(ct);
        var waliKelasAktif = taAktifId > 0
            ? await db.WaliKelas.Where(w => w.TahunAjaranId == taAktifId).ToDictionaryAsync(w => w.GuruId, w => w.KelasId, ct)
            : new Dictionary<int, int>();

        var kepalaSaatIni = taAktifId > 0
            ? await db.KepalaSekolah.Where(k => k.TahunAjaranId == taAktifId).Select(k => k.GuruId).ToListAsync(ct)
            : [];
        var kepalaSet = kepalaSaatIni.ToHashSet();

        var rows = guruList.Select(g => (object)new
        {
            SourceId = g.GuruId,
            g.Nama,
            g.Nip,
            JenisKelamin = g.JenisKelamin?.ToString(),
            Jabatan = g.Jabatan.ToString(),
            KelasSourceId = waliKelasAktif.TryGetValue(g.GuruId, out var kelasId) ? kelasId : (int?)null,
            g.NoHandphone,
            StatusAktif = g.StatusAktif ? 1 : 0,
            IsKepalaSekolah = kepalaSet.Contains(g.GuruId),
            StatusKeluar = g.StatusKeluar?.ToString(),
        }).ToList();
        await PostAsync(url, token, "/api/v1/sync/guru", rows, full: false, "Guru", ct);
    }

    private async Task PushWaliKelasAsync(string url, string token, CancellationToken ct)
    {
        // Riwayat SEMUA tahun ajaran dikirim sekaligus (full snapshot) - §7.3.
        var rows = (await db.WaliKelas.ToListAsync(ct)).Select(w => (object)new
        {
            SourceId = w.WaliKelasId,
            KelasSourceId = w.KelasId,
            GuruSourceId = w.GuruId,
            TahunAjaranSourceId = w.TahunAjaranId,
            w.Urutan,
        }).ToList();
        await PostAsync(url, token, "/api/v1/sync/wali-kelas", rows, full: true, "Wali Kelas", ct);
    }

    private async Task PushKepalaSekolahAsync(string url, string token, CancellationToken ct)
    {
        var rows = (await db.KepalaSekolah.ToListAsync(ct)).Select(k => (object)new
        {
            SourceId = k.KepalaSekolahId,
            GuruSourceId = k.GuruId,
            TahunAjaranSourceId = k.TahunAjaranId,
        }).ToList();
        await PostAsync(url, token, "/api/v1/sync/kepala-sekolah", rows, full: true, "Kepala Sekolah", ct);
    }

    private async Task PushJadwalPelajaranAsync(string url, string token, CancellationToken ct)
    {
        var rows = (await db.JadwalPelajaran.Include(j => j.JamPelajaran).ToListAsync(ct)).Select(j => (object)new
        {
            SourceId = j.JadwalPelajaranId,
            TahunAjaranSourceId = j.TahunAjaranId,
            Semester = j.Semester.ToString(),
            KelasSourceId = j.KelasId,
            MataPelajaranSourceId = j.MataPelajaranId,
            GuruSourceId = j.GuruId,
            JamPelajaranSourceId = j.JamPelajaranId,
            j.JamPelajaran.JamKe,
            Hari = j.JamPelajaran.Hari.ToString(),
            JamMulai = j.JamPelajaran.JamMulai.ToString("HH:mm"),
            JamSelesai = j.JamPelajaran.JamSelesai.ToString("HH:mm"),
        }).ToList();
        await PostAsync(url, token, "/api/v1/sync/jadwal-pelajaran", rows, full: true, "Jadwal Pelajaran", ct);
    }

    private async Task PushKalenderAkademikAsync(string url, string token, CancellationToken ct)
    {
        var rows = (await db.KalenderAkademik.ToListAsync(ct)).Select(k => (object)new
        {
            SourceId = k.KalenderAkademikId,
            TahunAjaranSourceId = k.TahunAjaranId,
            k.Judul,
            k.Kategori,
            k.Warna,
            TanggalMulai = k.TanggalMulai.ToString("yyyy-MM-dd"),
            TanggalSelesai = k.TanggalSelesai?.ToString("yyyy-MM-dd"),
            k.Waktu,
            k.Sasaran,
            IsLibur = k.IsLibur ? 1 : 0,
            k.Keterangan,
        }).ToList();
        await PostAsync(url, token, "/api/v1/sync/kalender-akademik", rows, full: true, "Kalender Akademik", ct);
    }

    private async Task PushCalonSiswaAsync(string url, string token, CancellationToken ct)
    {
        // Dikirim SEMUA status (bukan cuma 'menunggu') - perubahan lokal (TU proses
        // via web) ikut ter-update di Mobile-app. Nama file dokumen fisik TIDAK
        // PERNAH dikirim - cuma ada di disk PC ini.
        var rows = (await db.CalonSiswa.ToListAsync(ct)).Select(c => (object)new
        {
            SourceId = c.CalonSiswaId,
            c.Nama,
            JenisKelamin = c.JenisKelamin.ToString(),
            c.Nisn,
            c.TempatLahir,
            TanggalLahir = c.TanggalLahir?.ToString("yyyy-MM-dd"),
            c.AsalSekolah,
            c.AlamatJalan,
            c.AlamatKelurahan,
            c.AlamatKecamatan,
            c.NamaAyah,
            c.NamaIbu,
            c.NoHandphone,
            DokumenLengkap = (!string.IsNullOrEmpty(c.DokumenKk) && !string.IsNullOrEmpty(c.DokumenAkta) && !string.IsNullOrEmpty(c.DokumenKia) && !string.IsNullOrEmpty(c.DokumenIjazah)) ? 1 : 0,
            Status = c.Status.ToString(),
            c.Catatan,
        }).ToList();
        await PostAsync(url, token, "/api/v1/sync/psb", rows, full: false, "PSB/Calon Siswa", ct);
    }

    // ---------------------------------------------------------------- Pull PSB

    private record KeputusanPending(int Id, int CalonSiswaSourceId, string Keputusan, string? Nis, int? KelasSourceId, string? Catatan);

    private async Task PullKeputusanPsbAsync(string url, string token, CancellationToken ct)
    {
        List<KeputusanPending> items;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{url}/api/v1/psb/keputusan");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));
            using var resp = await http.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode) { logger.LogWarning("Pull keputusan PSB: GAGAL HTTP {Status}", (int)resp.StatusCode); return; }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("success", out var ok) || !ok.GetBoolean()) return;
            if (!doc.RootElement.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Array) return;

            items = dataEl.EnumerateArray().Select(el => new KeputusanPending(
                GetIntFlexible(el, "id") ?? 0,
                GetIntFlexible(el, "calon_siswa_source_id") ?? 0,
                el.GetProperty("keputusan").GetString()!,
                el.TryGetProperty("nis", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null,
                GetIntFlexible(el, "kelas_source_id"),
                el.TryGetProperty("catatan", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null)).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Pull keputusan PSB: GAGAL koneksi - {Message}", ex.Message);
            return;
        }

        foreach (var item in items)
        {
            string statusLapor;
            string? pesanGagal = null;

            var calon = await db.CalonSiswa.FindAsync([item.CalonSiswaSourceId], ct);
            if (calon is null)
            {
                statusLapor = "failed";
                pesanGagal = "Calon siswa tidak ditemukan di WebArsipData.";
            }
            // Idempotency guard - status lokal SUDAH sama dgn keputusan -> applied
            // tanpa proses ulang (§7.3), BUKAN dianggap gagal.
            else if (item.Keputusan == "terima" && calon.Status == StatusCalonSiswa.diterima)
            {
                statusLapor = "applied";
            }
            else if (item.Keputusan == "tolak" && calon.Status == StatusCalonSiswa.ditolak)
            {
                statusLapor = "applied";
            }
            else if (item.Keputusan == "terima")
            {
                var kelas = item.KelasSourceId is > 0 ? await db.Kelas.FindAsync([item.KelasSourceId.Value], ct) : null;
                if (kelas is null)
                {
                    statusLapor = "failed";
                    pesanGagal = "Kelas tujuan tidak ditemukan (mungkin sudah dihapus/diubah).";
                }
                else
                {
                    var hasil = await psb.TerimaAsync(item.CalonSiswaSourceId, item.Nis, kelas.KelasId);
                    statusLapor = hasil.Success ? "applied" : "failed";
                    pesanGagal = hasil.Success ? null : hasil.Message;
                }
            }
            else
            {
                var hasil = await psb.TolakAsync(item.CalonSiswaSourceId, item.Catatan);
                statusLapor = hasil.Success ? "applied" : "failed";
                pesanGagal = hasil.Success ? null : hasil.Message;
            }

            await LaporKeputusanSelesaiAsync(url, token, item.Id, statusLapor, pesanGagal, ct);
        }
    }

    private async Task LaporKeputusanSelesaiAsync(string url, string token, int id, string status, string? pesanGagal, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{url}/api/v1/psb/keputusan/{id}/selesai");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            req.Content = JsonContent.Create(new { status, pesan_gagal = pesanGagal });
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));
            using var resp = await http.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode) logger.LogWarning("Lapor keputusan PSB #{Id}: GAGAL HTTP {Status}", id, (int)resp.StatusCode);
        }
        catch (Exception ex)
        {
            // Gagal lapor balik BUKAN celah - keputusan tetap 'pending' di Hub API,
            // ditarik lagi siklus berikutnya, idempotent by design (§7.3).
            logger.LogWarning(ex, "Lapor keputusan PSB #{Id}: GAGAL koneksi - {Message}", id, ex.Message);
        }
    }
}
