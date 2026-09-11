using System.Net.Http.Json;
using System.Text.Json;

namespace DataMaster.Web.Services;

// Dipakai AuthController (Setup Awal) DAN UserController (menu Pengaturan,
// 2026-09-11) - dipisah jadi service bersama supaya "daftar/sambungkan ulang
// ke Hub API" TIDAK cuma bisa dilakukan sekali seumur hidup instalasi (dulu
// HANYA ada di Setup Awal, yang cuma tampil SEKALI selagi database kosong).
// Gap nyata ditemukan 2026-09-11: token Hub API bisa ke-reset kosong (lihat
// AppSettingsWriterService/ServerProcessManager) TANPA ada cara mengisi ulang
// lewat UI sama sekali sebelum perbaikan ini.
public class HubApiRegistrationService(IHttpClientFactory httpClientFactory, ILogger<HubApiRegistrationService> logger)
{
    // Mengembalikan token asli kalau berhasil, null kalau gagal (non-fatal -
    // pemanggil TIDAK BOLEH menggagalkan alur utama cuma krn ini gagal, mis.
    // internet mati saat itu - bisa dicoba lagi belakangan).
    public async Task<string?> DaftarAsync(string namaUnit)
    {
        try
        {
            using var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(15);
            using var resp = await http.PostAsJsonAsync($"{AppOptions.HubApiUrlResmi}/api/v1/register",
                new { kunci = AppOptions.RegisterSharedKey, nama = namaUnit });

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!resp.IsSuccessStatusCode || !root.TryGetProperty("success", out var ok) || !ok.GetBoolean())
            {
                var pesan = root.TryGetProperty("message", out var m) ? m.GetString() : body;
                logger.LogWarning("Pendaftaran Hub API ditolak: {Pesan}", pesan);
                return null;
            }

            return root.GetProperty("token").GetString();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Pendaftaran Hub API gagal - koneksi bermasalah.");
            return null;
        }
    }
}
