using System.Reflection;

namespace DataMaster.Web.Services;

// Versi aplikasi yang sedang berjalan (dicap dari tag rilis saat build, mis. "1.8.0"). Ditampilkan di menu samping
// dan halaman Setting, dan dikirim ke Hub API (header X-App-Version) supaya Admin IT bisa melihat dari jauh
// unit mana yang sudah ter-update. Build lokal/uji (0.0.0) -> "dev".
public static class AppVersionInfo
{
    public static string Current { get; } = Baca();

    // Versi resmi (x.y.z) atau null untuk build lokal - dipakai untuk header agar versi "dev" tidak dilaporkan.
    public static string? Resmi => Current == "dev" ? null : Current;

    private static string Baca()
    {
        var v = Assembly.GetEntryAssembly()?.GetName().Version;
        return v is null || (v.Major == 0 && v.Minor == 0 && v.Build <= 0) ? "dev" : v.ToString(3);
    }
}
