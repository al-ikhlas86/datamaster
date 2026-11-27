namespace DataMaster.Data.Psb;

/// <summary>
/// Tagihan awal calon siswa PSB (2026-11-28). Keuangan menghitung status (tanpa_tagihan | belum | sebagian | lunas) lalu mengirimnya lewat webview dan Hub ke sini.
/// Aturan murni (tanpa database/jaringan) supaya teruji: kapan calon boleh masuk kelas dan teks yang ditampilkan.
/// </summary>
public static class PsbTagihanAwal
{
    /// <summary>
    /// Calon BARU boleh masuk kelas bila tagihan awalnya TIDAK diketahui belum lunas. Hanya status "belum"/"sebagian" yang memblokir; null (Keuangan belum terhubung/belum ada kabar)
    /// dan "tanpa_tagihan" (staf belum mengatur jenis pembayaran awal) tidak memblokir, supaya sekolah yang belum memakai Keuangan tidak terkunci.
    /// </summary>
    public static bool BolehMasukKelas(string? status) => status is not ("belum" or "sebagian");

    /// <summary>"Lunas" hanya untuk status lunas: dipakai fitur penempatan kelas acak (calon yang sudah membayar saja).</summary>
    public static bool Lunas(string? status) => status == "lunas";

    public static string Label(string? status) => status switch
    {
        "lunas" => "Lunas",
        "sebagian" => "Dibayar sebagian",
        "belum" => "Belum dibayar",
        "tanpa_tagihan" => "Belum ada tagihan",
        _ => "Belum ada kabar dari Keuangan",
    };

    public static string PesanBlokir(string? status, decimal? total, decimal? dibayar)
    {
        var rincian = total is > 0 ? $" (dibayar Rp {dibayar ?? 0:N0} dari Rp {total:N0})" : "";
        return $"Tagihan awal calon ini belum lunas{rincian}. Calon baru bisa masuk kelas setelah lunas di Keuangan.";
    }
}
