using System.IO;

namespace DataMaster.Launcher;

// Rotasi berkas log HARIAN (web_*.log dari ServerProcessManager, launcher_*.log
// dari App.xaml.cs) - TANPA ini, aplikasi yang dimaksudkan jalan bertahun-tahun
// di PC sekolah ("super awet jangka panjang") akan menumpuk 1 berkas baru SETIAP
// HARI selamanya tanpa pernah dihapus - bug nyata ditemukan lewat audit terpisah
// (bukan uji end-to-end fungsional), pola SAMA dgn rotasi backup manual/antrian
// yang sudah ada di DatabaseBackupService, hanya beda kriteria (umur hari,
// bukan jumlah berkas, krn nama file di sini per-tanggal bukan per-kejadian).
public static class LogCleanup
{
    private const int SimpanHari = 14;

    public static void RotasiLogLama(string logDir)
    {
        try
        {
            var batas = DateTime.Now.AddDays(-SimpanHari);
            foreach (var f in Directory.GetFiles(logDir, "*.log"))
            {
                try
                {
                    if (File.GetLastWriteTime(f) < batas) File.Delete(f);
                }
                catch { /* berkas mungkin sedang dipakai proses lain - coba lagi siklus berikutnya */ }
            }
        }
        catch { /* jangan sampai gagal rotasi log menghalangi start aplikasi */ }
    }
}
