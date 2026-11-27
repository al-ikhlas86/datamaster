using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DataMaster.Data.Psb;
using Microsoft.Extensions.Options;

namespace DataMaster.Web.Services;

// Kelola PSB dari Data Master (2026-11-28): meminta tiket masuk sekali pakai ke Hub API atas nama unit sekolah ini (unit ditentukan Hub dari token,
// bukan dari kita), lalu menyusun URL halaman PSB webview. Hak yang diperoleh di sana dibatasi server pada katalog unit ini (SD tidak melihat TK).
public sealed record HasilTiketPsb(string? Url, string? Pesan);

public class PsbKelolaService(HttpClient http, IOptions<AppOptions> options, ILogger<PsbKelolaService> logger)
{
    public async Task<HasilTiketPsb> MintaAsync(string? namaPengguna, CancellationToken ct = default)
    {
        var url = (options.Value.HubApiUrl ?? "").Trim().TrimEnd('/');
        var token = (options.Value.HubApiToken ?? "").Trim();
        if (url == "" || token == "") return new(null, "Data Master ini belum terhubung ke Hub API. Selesaikan Setup Awal atau hubungi Admin IT.");
        var web = PsbKelola.AlamatWeb(url);
        if (web is null) return new(null, "Alamat server tidak dikenali. Hubungi Admin IT.");

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{url}/api/v1/psb/tiket");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            HubHeaders.Terapkan(req);
            req.Content = new StringContent(JsonSerializer.Serialize(new { nama = PsbKelola.BersihkanNama(namaPengguna) }), Encoding.UTF8, "application/json");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));
            using var resp = await http.SendAsync(req, cts.Token);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) return new(null, "Perangkat ini belum/tidak lagi disetujui Admin IT. Hubungi Admin IT.");
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Kelola PSB: Hub menolak tiket, HTTP {Status}", (int)resp.StatusCode);
                return new(null, "Server menolak permintaan. Coba lagi sebentar atau hubungi Admin IT.");
            }
            var masuk = PsbKelola.UrlMasuk(web, PsbKelola.BacaTiket(body));
            return masuk is null ? new(null, "Jawaban server tidak dikenali. Pastikan Hub API sudah diperbarui.") : new(masuk, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning("Kelola PSB: tidak bisa menghubungi Hub ({Tipe})", ex.GetType().Name);
            return new(null, "Tidak bisa menghubungi server. Cek koneksi internet lalu coba lagi.");
        }
    }
}
