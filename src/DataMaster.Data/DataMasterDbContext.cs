using DataMaster.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataMaster.Data;

public class DataMasterDbContext : DbContext
{
    public DataMasterDbContext(DbContextOptions<DataMasterDbContext> options) : base(options)
    {
    }

    public DbSet<TahunAjaran> TahunAjaran => Set<TahunAjaran>();
    public DbSet<Tingkat> Tingkat => Set<Tingkat>();
    public DbSet<Kelas> Kelas => Set<Kelas>();
    public DbSet<Siswa> Siswa => Set<Siswa>();
    public DbSet<CalonSiswa> CalonSiswa => Set<CalonSiswa>();
    public DbSet<Guru> Guru => Set<Guru>();
    public DbSet<WaliKelas> WaliKelas => Set<WaliKelas>();
    public DbSet<KepalaSekolah> KepalaSekolah => Set<KepalaSekolah>();
    public DbSet<Ekskul> Ekskul => Set<Ekskul>();
    public DbSet<EkskulSiswa> EkskulSiswa => Set<EkskulSiswa>();
    public DbSet<MataPelajaran> MataPelajaran => Set<MataPelajaran>();
    public DbSet<GuruMataPelajaran> GuruMataPelajaran => Set<GuruMataPelajaran>();
    public DbSet<KurikulumAlokasi> KurikulumAlokasi => Set<KurikulumAlokasi>();
    public DbSet<JamPelajaran> JamPelajaran => Set<JamPelajaran>();
    public DbSet<JamPelajaranTingkat> JamPelajaranTingkat => Set<JamPelajaranTingkat>();
    public DbSet<JadwalPelajaran> JadwalPelajaran => Set<JadwalPelajaran>();
    public DbSet<JadwalPiket> JadwalPiket => Set<JadwalPiket>();
    public DbSet<KalenderAkademik> KalenderAkademik => Set<KalenderAkademik>();
    public DbSet<RiwayatAkademik> RiwayatAkademik => Set<RiwayatAkademik>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuthGroup> AuthGroups => Set<AuthGroup>();
    public DbSet<AuthGroupUser> AuthGroupUsers => Set<AuthGroupUser>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Semua enum disimpan sbg TEXT (bukan integer) - lihat alasan di Enums.cs.
        // Nilai string SAMA PERSIS nama constant PHP asli (mis. "aktif", "guru_kelas",
        // "purna_bakti") supaya payload sync ke Hub API tidak perlu tabel terjemahan.

        modelBuilder.Entity<TahunAjaran>(e =>
        {
            e.HasKey(x => x.TahunAjaranId);
            e.HasIndex(x => x.Nama).IsUnique();
            // Format "YYYY/YYYY" divalidasi di service layer (controller PHP asli
            // juga tidak menegakkannya di level DB) - lihat 02-guru-kelas-struktur.md §9.5.
        });

        modelBuilder.Entity<Tingkat>(e =>
        {
            e.HasKey(x => x.TingkatId);
            e.HasIndex(x => x.Kode).IsUnique();
        });

        modelBuilder.Entity<Kelas>(e =>
        {
            e.HasKey(x => x.KelasId);
            // Tingkat SENGAJA string bebas, bukan FK - lihat komentar entity Tingkat.cs.
            e.HasIndex(x => new { x.Tingkat, x.NamaKelas }).IsUnique();
        });

        modelBuilder.Entity<Siswa>(e =>
        {
            e.HasKey(x => x.SiswaId);
            e.HasIndex(x => x.Nis).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.KelasId);
            e.Property(x => x.JenisKelamin).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Kelas)
                .WithMany(k => k.SiswaList)
                .HasForeignKey(x => x.KelasId)
                .OnDelete(DeleteBehavior.SetNull);
            // Siswa TIDAK PERNAH dihapus fisik di PHP asli (arsip via Status='keluar')
            // - tidak ada operasi delete yang perlu direncanakan cascade-nya di sini.
        });

        modelBuilder.Entity<CalonSiswa>(e =>
        {
            e.HasKey(x => x.CalonSiswaId);
            e.HasIndex(x => x.Status);
            e.Property(x => x.JenisKelamin).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Siswa)
                .WithOne(s => s.CalonSiswaAsal)
                .HasForeignKey<CalonSiswa>(x => x.SiswaId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Guru>(e =>
        {
            e.HasKey(x => x.GuruId);
            e.HasIndex(x => x.Nip);
            e.HasIndex(x => x.NomorUrut).IsUnique();
            // NoHandphone SENGAJA TANPA unique index di level DB - lihat catatan
            // bug nyata "uniq_guru_no_handphone" di komentar entity Guru.cs. Keunikan
            // dicek di service layer, scoped ke StatusAktif=true saja.
            e.Property(x => x.JenisKelamin).HasConversion<string>();
            e.Property(x => x.Jabatan).HasConversion<string>();
            e.Property(x => x.StatusKeluar).HasConversion<string>();
            e.HasOne(x => x.TahunAjaranMulai)
                .WithMany(t => t.GuruMulai)
                .HasForeignKey(x => x.TahunAjaranMulaiId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WaliKelas>(e =>
        {
            e.HasKey(x => x.WaliKelasId);
            e.HasIndex(x => new { x.GuruId, x.TahunAjaranId }).IsUnique();
            e.HasIndex(x => new { x.KelasId, x.TahunAjaranId, x.Urutan }).IsUnique();
            e.HasOne(x => x.Kelas).WithMany(k => k.WaliKelasList).HasForeignKey(x => x.KelasId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Guru).WithMany(g => g.WaliKelasList).HasForeignKey(x => x.GuruId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.TahunAjaran).WithMany(t => t.WaliKelasList).HasForeignKey(x => x.TahunAjaranId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KepalaSekolah>(e =>
        {
            e.HasKey(x => x.KepalaSekolahId);
            e.HasIndex(x => new { x.GuruId, x.TahunAjaranId }).IsUnique();
            e.HasOne(x => x.Guru).WithMany(g => g.KepalaSekolahList).HasForeignKey(x => x.GuruId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.TahunAjaran).WithMany(t => t.KepalaSekolahList).HasForeignKey(x => x.TahunAjaranId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Ekskul>(e =>
        {
            e.HasKey(x => x.EkskulId);
        });

        modelBuilder.Entity<EkskulSiswa>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EkskulId, x.SiswaId }).IsUnique();
            e.HasOne(x => x.Ekskul).WithMany(k => k.EkskulSiswaList).HasForeignKey(x => x.EkskulId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Siswa).WithMany(s => s.EkskulSiswaList).HasForeignKey(x => x.SiswaId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MataPelajaran>(e =>
        {
            e.HasKey(x => x.MataPelajaranId);
            e.HasIndex(x => x.Nama).IsUnique();
            e.HasIndex(x => x.Kode).IsUnique();
        });

        modelBuilder.Entity<GuruMataPelajaran>(e =>
        {
            e.HasKey(x => x.GuruMataPelajaranId);
            // WAJIB PERSIS (GuruId,MataPelajaranId,Tingkat) - JANGAN tambahkan unique
            // 2-kolom terpisah, itu bug yang sudah diperbaiki di PHP asli (lihat
            // komentar entity GuruMataPelajaran.cs).
            e.HasIndex(x => new { x.GuruId, x.MataPelajaranId, x.Tingkat }).IsUnique();
            e.HasOne(x => x.Guru).WithMany(g => g.GuruMataPelajaranList).HasForeignKey(x => x.GuruId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.MataPelajaran).WithMany(m => m.GuruMataPelajaranList).HasForeignKey(x => x.MataPelajaranId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KurikulumAlokasi>(e =>
        {
            e.HasKey(x => x.KurikulumAlokasiId);
            e.HasIndex(x => new { x.TahunAjaranId, x.TingkatKode, x.MataPelajaranId }).IsUnique();
            e.HasOne(x => x.TahunAjaran).WithMany(t => t.KurikulumAlokasiList).HasForeignKey(x => x.TahunAjaranId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.MataPelajaran).WithMany(m => m.KurikulumAlokasiList).HasForeignKey(x => x.MataPelajaranId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JamPelajaran>(e =>
        {
            e.HasKey(x => x.JamPelajaranId);
            e.HasIndex(x => new { x.TahunAjaranId, x.Hari, x.JamKe }).IsUnique();
            e.Property(x => x.Hari).HasConversion<string>();
            e.Property(x => x.Jenis).HasConversion<string>();
            e.HasOne(x => x.TahunAjaran).WithMany(t => t.JamPelajaranList).HasForeignKey(x => x.TahunAjaranId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JamPelajaranTingkat>(e =>
        {
            e.HasKey(x => x.JamPelajaranTingkatId);
            e.HasIndex(x => new { x.JamPelajaranId, x.TingkatKode }).IsUnique();
            e.HasOne(x => x.JamPelajaran).WithMany(j => j.BatasanTingkat).HasForeignKey(x => x.JamPelajaranId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JadwalPelajaran>(e =>
        {
            e.HasKey(x => x.JadwalPelajaranId);
            e.HasIndex(x => new { x.TahunAjaranId, x.Semester, x.KelasId, x.JamPelajaranId }).IsUnique();
            e.HasIndex(x => new { x.GuruId, x.JamPelajaranId, x.Semester });
            e.Property(x => x.Semester).HasConversion<string>();
            e.HasOne(x => x.TahunAjaran).WithMany(t => t.JadwalPelajaranList).HasForeignKey(x => x.TahunAjaranId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Kelas).WithMany(k => k.JadwalPelajaranList).HasForeignKey(x => x.KelasId).OnDelete(DeleteBehavior.Cascade);
            // JamPelajaranId CASCADE (hapus jam ikut menghapus jadwal), MataPelajaranId
            // RESTRICT (tidak bisa hapus mapel yang masih dipakai jadwal) - 2 perilaku
            // FK SANGAT BERBEDA, lihat 03-akademik-jadwal.md §10 poin K.
            e.HasOne(x => x.JamPelajaran).WithMany(j => j.JadwalPelajaranList).HasForeignKey(x => x.JamPelajaranId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.MataPelajaran).WithMany(m => m.JadwalPelajaranList).HasForeignKey(x => x.MataPelajaranId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Guru).WithMany(g => g.JadwalPelajaranList).HasForeignKey(x => x.GuruId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<JadwalPiket>(e =>
        {
            e.HasKey(x => x.JadwalPiketId);
            e.HasIndex(x => new { x.TahunAjaranId, x.Semester, x.Hari, x.GuruId }).IsUnique();
            e.Property(x => x.Semester).HasConversion<string>();
            e.Property(x => x.Hari).HasConversion<string>();
            e.HasOne(x => x.TahunAjaran).WithMany(t => t.JadwalPiketList).HasForeignKey(x => x.TahunAjaranId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Guru).WithMany(g => g.JadwalPiketList).HasForeignKey(x => x.GuruId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KalenderAkademik>(e =>
        {
            e.HasKey(x => x.KalenderAkademikId);
            e.HasIndex(x => new { x.TahunAjaranId, x.TanggalMulai });
            e.HasOne(x => x.TahunAjaran).WithMany(t => t.KalenderAkademikList).HasForeignKey(x => x.TahunAjaranId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RiwayatAkademik>(e =>
        {
            e.HasKey(x => x.RiwayatAkademikId);
            e.HasIndex(x => new { x.SiswaId, x.TahunAjaranId }).IsUnique();
            e.HasIndex(x => x.TahunAjaranId);
            e.HasIndex(x => x.KelasId);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Siswa).WithMany(s => s.RiwayatAkademikList).HasForeignKey(x => x.SiswaId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.TahunAjaran).WithMany(t => t.RiwayatAkademikList).HasForeignKey(x => x.TahunAjaranId).OnDelete(DeleteBehavior.Cascade);
            // KelasId SENGAJA tanpa FK (murni snapshot) - lihat komentar entity.
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.UserId);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<AuthGroup>(e =>
        {
            e.HasKey(x => x.AuthGroupId);
            // UNIQUE di Name SENGAJA dipasang sejak awal (PHP asli TIDAK punya ini -
            // pernah menyebabkan bug nyata 4 baris "admin" duplikat, lihat komentar
            // entity User.cs / 04-infra-auth-sync.md §12).
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<AuthGroupUser>(e =>
        {
            e.HasKey(x => new { x.UserId, x.AuthGroupId });
            e.HasOne(x => x.User).WithMany(u => u.GroupMemberships).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.AuthGroup).WithMany(g => g.Members).HasForeignKey(x => x.AuthGroupId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SystemSetting>(e =>
        {
            e.HasKey(x => x.SettingKey);
        });
    }
}
