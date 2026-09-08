using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace DataMaster.Web.Services;

// Port dari App\Libraries\LoginThrottle (PHP) - lihat 04-infra-auth-sync.md §1.4.
// BUKAN lockout permanen (sengaja) - lockout permanen bisa disalahgunakan utk
// mengunci akun ORANG LAIN cukup dgn salah password berkali-kali. Backoff bertahap
// berbasis cache, key = md5(login value), TTL 1 jam, direset begitu login sukses.
public class LoginThrottleService(IMemoryCache cache)
{
    private static string CacheKey(string loginValue) =>
        "login_throttle_" + Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(loginValue.Trim().ToLowerInvariant())));

    // Return detik tunggu tersisa (0 = boleh coba sekarang).
    public int SisaTungguDetik(string loginValue)
    {
        if (!cache.TryGetValue<(int Attempts, DateTime? WaitUntil)>(CacheKey(loginValue), out var state)) return 0;
        if (state.WaitUntil is null) return 0;
        var sisa = (int)Math.Ceiling((state.WaitUntil.Value - DateTime.UtcNow).TotalSeconds);
        return sisa > 0 ? sisa : 0;
    }

    public void CatatGagal(string loginValue)
    {
        var key = CacheKey(loginValue);
        var state = cache.TryGetValue<(int Attempts, DateTime? WaitUntil)>(key, out var existing) ? existing : (Attempts: 0, WaitUntil: (DateTime?)null);
        var attempts = state.Attempts + 1;

        DateTime? waitUntil = attempts switch
        {
            >= 7 => DateTime.UtcNow.AddSeconds(300),
            >= 5 => DateTime.UtcNow.AddSeconds(60),
            >= 3 => DateTime.UtcNow.AddSeconds(30),
            _ => null,
        };

        cache.Set(key, (Attempts: attempts, WaitUntil: waitUntil), TimeSpan.FromSeconds(3600));
    }

    public void Reset(string loginValue) => cache.Remove(CacheKey(loginValue));
}
