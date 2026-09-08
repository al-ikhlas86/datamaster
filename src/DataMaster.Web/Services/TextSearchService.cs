namespace DataMaster.Web.Services;

// Port fungsional dari app/Libraries/BoyerMoore.php (dipakai Siswa::index() dan
// Guru::index() mode pencarian - lihat 01-siswa-psb.md §4, 02-guru-kelas-struktur.md
// §3.2). PHP asli mengimplementasikan algoritma Boyer-Moore murni (Occurrence +
// Match Heuristic) untuk mencari substring case-insensitive; behavior yang harus
// direplikasi HANYA hasilnya (substring match, case-insensitive, cocok di SALAH
// SATU field yang diminta) - bukan implementasi algoritmanya sendiri, karena
// `string.Contains(..., StringComparison.OrdinalIgnoreCase)` bawaan .NET sudah
// benar & performan untuk ukuran data 1 sekolah (ratusan-ribuan baris).
public static class TextSearchService
{
    public static bool MatchesAny(string keyword, params string?[] fields)
    {
        if (string.IsNullOrEmpty(keyword)) return true;
        foreach (var f in fields)
        {
            if (!string.IsNullOrEmpty(f) && f.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
