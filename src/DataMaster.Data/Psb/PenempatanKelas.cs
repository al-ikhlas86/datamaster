namespace DataMaster.Data.Psb;

/// <summary>
/// Penempatan acak siswa baru ke kelas paralel (2026-11-28). Murni (tanpa database): kelas diisi merata dan komposisi laki-laki/perempuan diseimbangkan,
/// dengan urutan calon diacak dari seed. Hasil hanya usulan: staf meninjau lalu menerapkan.
/// </summary>
public static class PenempatanKelas
{
    public sealed record Calon(int Id, bool Laki);

    /// <summary>Kelas tujuan dengan isi yang SUDAH ada (siswa aktif) supaya kelas yang sudah terisi tidak dibanjiri.</summary>
    public sealed record Kelas(int Id, int Terisi, int LakiTerisi);

    public sealed record Usulan(int CalonId, int KelasId);

    /// <summary>Seed sama + masukan sama = hasil sama (bisa diulang/diuji); seed beda = pengacakan baru.</summary>
    public static List<Usulan> Susun(IReadOnlyList<Calon> calon, IReadOnlyList<Kelas> kelas, int seed)
    {
        var hasil = new List<Usulan>();
        if (calon.Count == 0 || kelas.Count == 0) return hasil;

        var rng = new Random(seed);
        var isi = kelas.Select(k => new Slot(k.Id, k.Terisi, k.LakiTerisi)).ToList();

        // Urutan stabil sebelum diacak (masukan boleh datang dalam urutan apa pun, hasil hanya bergantung pada seed).
        List<Calon> Acak(IEnumerable<Calon> sumber)
        {
            var l = sumber.OrderBy(c => c.Id).ToList();
            for (var i = l.Count - 1; i > 0; i--) { var j = rng.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); }
            return l;
        }

        void Tempatkan(Calon c, Slot s)
        {
            s.Terisi++;
            if (c.Laki) s.Laki++;
            hasil.Add(new Usulan(c.Id, s.Id));
        }

        // Laki-laki lebih dulu: sebar rata menurut jumlah laki-laki (seri -> kelas yang paling sedikit terisi). Lalu perempuan: sebar menurut ukuran kelas
        // (menutup selisih yang tersisa dari laki-laki), seri -> kelas yang paling sedikit perempuan. Hasil: ukuran kelas rata DAN komposisi kelamin berimbang.
        foreach (var c in Acak(calon.Where(x => x.Laki)))
            Tempatkan(c, isi.OrderBy(s => s.Laki).ThenBy(s => s.Terisi).ThenBy(_ => rng.Next()).First());
        foreach (var c in Acak(calon.Where(x => !x.Laki)))
            Tempatkan(c, isi.OrderBy(s => s.Terisi).ThenBy(s => s.Terisi - s.Laki).ThenBy(_ => rng.Next()).First());
        return hasil;
    }

    private sealed class Slot(int id, int terisi, int laki)
    {
        public int Id { get; } = id;
        public int Terisi { get; set; } = terisi;
        public int Laki { get; set; } = laki;
    }
}
