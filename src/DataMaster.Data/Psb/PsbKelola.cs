using System.Text.Json;
using System.Text.RegularExpressions;

namespace DataMaster.Data.Psb;

// Kelola PSB dari Data Master (2026-11-28): Data Master meminta tiket sekali pakai ke Hub API lalu membuka halaman PSB webview dengan tiket itu.
// Bagian murni (tanpa jaringan) ada di sini supaya teruji: turunan alamat webview, pembersihan nama, pembacaan balasan Hub, dan penyusunan URL.
public static partial class PsbKelola
{
    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex PolaTiket();

    [GeneratedRegex("[\\u0000-\\u001f\\u007f]+")]
    private static partial Regex Kontrol();

    /// <summary>Alamat webview (tempat halaman PSB) diturunkan dari alamat Hub API: "https://host/hub-api" menjadi "https://host". Null bila bukan https/http sah.</summary>
    public static string? AlamatWeb(string? hubApiUrl)
    {
        var u = (hubApiUrl ?? "").Trim().TrimEnd('/');
        if (u.EndsWith("/hub-api", StringComparison.OrdinalIgnoreCase)) u = u[..^"/hub-api".Length];
        if (!Uri.TryCreate(u, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return null;
        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo)) return null;
        return u;
    }

    /// <summary>Nama pengguna untuk jejak audit di server: tanpa karakter kontrol, maksimal 80 karakter; kosong menjadi null.</summary>
    public static string? BersihkanNama(string? nama)
    {
        var t = Kontrol().Replace(nama ?? "", " ").Trim();
        if (t.Length == 0) return null;
        return t.Length > 80 ? t[..80] : t;
    }

    /// <summary>Mengambil tiket dari balasan Hub ({"success":true,"data":{"tiket":"..."}}); null bila bentuknya tidak sah.</summary>
    public static string? BacaTiket(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object || !r.TryGetProperty("success", out var s) || s.ValueKind != JsonValueKind.True) return null;
            if (!r.TryGetProperty("data", out var d) || d.ValueKind != JsonValueKind.Object || !d.TryGetProperty("tiket", out var t) || t.ValueKind != JsonValueKind.String) return null;
            var tiket = t.GetString();
            return tiket is not null && PolaTiket().IsMatch(tiket) ? tiket : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>URL halaman Kelola PSB di webview; null bila alamat webview atau tiket tidak sah.</summary>
    public static string? UrlMasuk(string? alamatWeb, string? tiket)
    {
        if (alamatWeb is null || tiket is null || !PolaTiket().IsMatch(tiket)) return null;
        return $"{alamatWeb.TrimEnd('/')}/psb-dm?tiket={tiket}";
    }
}
