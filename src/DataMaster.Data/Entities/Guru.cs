namespace DataMaster.Data.Entities;

// guru - Jabatan guru_kelas/guru_bidang TIDAK PERNAH diketik manual (derivasi
// otomatis dari WaliKelasModel::sinkronJabatanGuru(), lihat 02-guru-kelas-struktur.md
// §11). NoHandphone TANPA unique constraint di level database SENGAJA (lihat
// §12 poin 6 di spec yang sama) - keunikan HANYA dicek di layer aplikasi, scoped ke
// StatusAktif=true saja (nomor HP guru yang diarsipkan bebas dipakai ulang).
// TahunAjaranMulaiId ADA di skema tapi TIDAK PERNAH diisi dari form manapun di PHP
// asli - dipertahankan sebagai kolom nullable untuk kompatibilitas skema, JANGAN
// ditambahkan UI untuk mengisinya kecuali diminta eksplisit (lihat §12 poin 23).
public class Guru
{
    public int GuruId { get; set; }
    public required string Nama { get; set; }
    public string? Nip { get; set; }
    public int? NomorUrut { get; set; }
    public JenisKelamin? JenisKelamin { get; set; }
    public JabatanGuru Jabatan { get; set; }
    public string? MataPelajaranCatatan { get; set; }
    public short? JumlahJamMengajar { get; set; }
    public string? GelarTerakhir { get; set; }
    public string? NoHandphone { get; set; }
    public string? Alamat { get; set; }
    public bool StatusAktif { get; set; } = true;
    public StatusKeluarGuru? StatusKeluar { get; set; }
    public int? TahunAjaranMulaiId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public TahunAjaran? TahunAjaranMulai { get; set; }
    public ICollection<WaliKelas> WaliKelasList { get; set; } = new List<WaliKelas>();
    public ICollection<KepalaSekolah> KepalaSekolahList { get; set; } = new List<KepalaSekolah>();
    public ICollection<GuruMataPelajaran> GuruMataPelajaranList { get; set; } = new List<GuruMataPelajaran>();
    public ICollection<JadwalPelajaran> JadwalPelajaranList { get; set; } = new List<JadwalPelajaran>();
    public ICollection<JadwalPiket> JadwalPiketList { get; set; } = new List<JadwalPiket>();
}
