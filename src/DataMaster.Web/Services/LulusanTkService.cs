using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace DataMaster.Web.Services;

// Kontinuitas TK->SD (2026-09-11, poin #12) - sisi KONSUMEN dari
// GET /api/v1/siswa/lulusan (lihat LulusanController.php di Hub API utk
// aturan aksesnya). Dipakai CalonSiswaController - HANYA relevan di
// instalasi yang dikonfigurasi Hub API-nya sbg tujuan (mis. SD) lewat
// psb.lulusanLink; instalasi yang tidak dikonfigurasi cukup menerima
// balasan kosong dari Hub API, TIDAK error.
public record LulusanTkRow(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("unit_id_asal")] int UnitIdAsal,
    [property: JsonPropertyName("nama")] string Nama,
    [property: JsonPropertyName("jenis_kelamin")] string JenisKelamin,
    [property: JsonPropertyName("nisn")] string? Nisn,
    [property: JsonPropertyName("hp_ortu")] string? HpOrtu,
    [property: JsonPropertyName("hp_ortu_kedua")] string? HpOrtuKedua);

public class LulusanTkService(HttpClient http, IOptions<AppOptions> options, ILogger<LulusanTkService> logger)
{
    // Balikin list kosong (BUKAN exception) utk semua kegagalan - internet
    // mati/Hub API belum dikonfigurasi/belum ada unit sumber diizinkan SEMUA
    // harus terasa sama di UI ("belum ada data"), bukan bikin halaman PSB
    // ikut error gara2 fitur tambahan ini.
    public async Task<List<LulusanTkRow>> AmbilAsync(CancellationToken ct = default)
    {
        var url = (options.Value.HubApiUrl ?? "").Trim().TrimEnd('/');
        var token = (options.Value.HubApiToken ?? "").Trim();
        if (url == "" || token == "") return [];

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{url}/api/v1/siswa/lulusan");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));
            using var resp = await http.SendAsync(req, cts.Token);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) return [];

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("data", out var dataEl)) return [];
            return JsonSerializer.Deserialize<List<LulusanTkRow>>(dataEl.GetRawText()) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gagal mengambil daftar lulusan TK dari Hub API.");
            return [];
        }
    }
}
