namespace DataMaster.Data.Entities;

// jam_pelajaran_tingkat - KOSONG (tidak ada baris untuk 1 JamPelajaranId) berarti
// jam/kegiatan itu berlaku untuk SEMUA tingkat. Unique (JamPelajaranId,TingkatKode).
public class JamPelajaranTingkat
{
    public int JamPelajaranTingkatId { get; set; }
    public int JamPelajaranId { get; set; }
    public required string TingkatKode { get; set; }

    public JamPelajaran JamPelajaran { get; set; } = null!;
}
