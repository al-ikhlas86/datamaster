using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace DataMaster.Launcher;

// Pengganti UPDATE.bat (cek `git fetch` + compare SHA) - lihat 04-infra-auth-sync.md
// §12. ADAPTASI SENGAJA & DIDOKUMENTASIKAN: aplikasi ini didistribusikan sbg build
// terkompilasi (bukan clone git di tiap PC sekolah spt PHP asli), jadi "versi
// terpasang vs terbaru" dibandingkan via tag rilis GitHub, BUKAN git SHA.
// LINGKUP SAAT INI: HANYA mengecek & memberi tahu (tautan ke halaman rilis) -
// unduh+pasang OTOMATIS BELUM diimplementasikan karena bergantung pipeline
// CI/CD yang menerbitkan aset rilis terstruktur (belum dibangun saat kode ini
// ditulis - lihat status di spec/00-INDEX.md). Pola "update.lock dgn stale-timeout"
// PHP TETAP WAJIB direplikasi persis begitu unduh+pasang otomatis dikerjakan -
// JANGAN biarkan lock tanpa batas waktu (bug nyata PHP 2026-09-02, §12).
public class UpdateChecker
{
    private const string RepoApiUrl = "https://api.github.com/repos/al-ikhlas86/datamaster-desktop/releases/latest";

    public record UpdateInfo(bool AdaPembaruan, string VersiTerpasang, string? VersiTerbaru, string? UrlRilis);

    public async Task<UpdateInfo> CekAsync(CancellationToken ct)
    {
        var versiTerpasang = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DataMasterLauncher", versiTerpasang));
            using var resp = await http.GetAsync(RepoApiUrl, ct);
            if (!resp.IsSuccessStatusCode) return new UpdateInfo(false, versiTerpasang, null, null);

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var tag = doc.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            var url = doc.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() : null;
            if (tag is null) return new UpdateInfo(false, versiTerpasang, null, null);

            var tagBersih = tag.TrimStart('v', 'V');
            var adaPembaruan = Version.TryParse(tagBersih, out var vTerbaru) && Version.TryParse(versiTerpasang, out var vTerpasang) && vTerbaru > vTerpasang;

            return new UpdateInfo(adaPembaruan, versiTerpasang, tagBersih, url);
        }
        catch
        {
            // Gagal cek (internet mati/GitHub tidak terjangkau) BUKAN error fatal -
            // aplikasi tetap jalan normal, cek lagi di kesempatan berikutnya.
            return new UpdateInfo(false, versiTerpasang, null, null);
        }
    }
}
