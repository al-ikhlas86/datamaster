using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace DataMaster.Web.Services;

// Pengenal PC untuk Hub API (2026-11-26): dikirim lewat header X-Device-Uid pada setiap sinkronisasi dan di body pendaftaran, supaya Admin IT
// bisa memblokir PC tertentu (unit yang diblokir tidak bisa mendaftar ulang sampai blokir dibuka). Diturunkan dari MachineGuid Windows
// lalu di-hash, jadi tidak memuat data pribadi dan bukan kredensial (tidak memberi akses apa pun). Sama dengan pola Presensi v1.6.0.
public static class DeviceIdentity
{
    private static readonly Lazy<string?> uid = new(Hitung);

    /// <summary>Pengenal stabil per PC, atau null bila tidak bisa dibaca (Hub lalu memperlakukannya seperti versi lama).</summary>
    public static string? Uid => uid.Value;

    private static string? Hitung()
    {
        try
        {
            string sumber;
            try
            {
                using var dasar = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var kunci = dasar.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                sumber = kunci?.GetValue("MachineGuid") as string ?? Environment.MachineName;
            }
            catch
            {
                sumber = Environment.MachineName;
            }
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes("datamaster|" + sumber));
            return "PC-" + Convert.ToHexString(hash)[..20];
        }
        catch
        {
            return null;
        }
    }
}
