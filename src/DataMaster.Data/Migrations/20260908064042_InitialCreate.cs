using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMaster.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthGroups",
                columns: table => new
                {
                    AuthGroupId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthGroups", x => x.AuthGroupId);
                });

            migrationBuilder.CreateTable(
                name: "Ekskul",
                columns: table => new
                {
                    EkskulId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nama = table.Column<string>(type: "TEXT", nullable: false),
                    Pembina = table.Column<string>(type: "TEXT", nullable: true),
                    Hari = table.Column<string>(type: "TEXT", nullable: true),
                    Deskripsi = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ekskul", x => x.EkskulId);
                });

            migrationBuilder.CreateTable(
                name: "Kelas",
                columns: table => new
                {
                    KelasId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NamaKelas = table.Column<string>(type: "TEXT", nullable: false),
                    Tingkat = table.Column<string>(type: "TEXT", nullable: false),
                    Kelompok = table.Column<byte>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kelas", x => x.KelasId);
                });

            migrationBuilder.CreateTable(
                name: "MataPelajaran",
                columns: table => new
                {
                    MataPelajaranId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nama = table.Column<string>(type: "TEXT", nullable: false),
                    Kode = table.Column<string>(type: "TEXT", nullable: true),
                    Kelompok = table.Column<string>(type: "TEXT", nullable: true),
                    Urutan = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MataPelajaran", x => x.MataPelajaranId);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    SettingKey = table.Column<string>(type: "TEXT", nullable: false),
                    SettingValue = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.SettingKey);
                });

            migrationBuilder.CreateTable(
                name: "TahunAjaran",
                columns: table => new
                {
                    TahunAjaranId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nama = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TahunAjaran", x => x.TahunAjaranId);
                });

            migrationBuilder.CreateTable(
                name: "Tingkat",
                columns: table => new
                {
                    TingkatId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kode = table.Column<string>(type: "TEXT", nullable: false),
                    Nama = table.Column<string>(type: "TEXT", nullable: false),
                    Urutan = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tingkat", x => x.TingkatId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: true),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    UserImage = table.Column<string>(type: "TEXT", nullable: true),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    ForcePassReset = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Siswa",
                columns: table => new
                {
                    SiswaId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nama = table.Column<string>(type: "TEXT", nullable: false),
                    JenisKelamin = table.Column<string>(type: "TEXT", nullable: false),
                    Nis = table.Column<string>(type: "TEXT", nullable: false),
                    Nisn = table.Column<string>(type: "TEXT", nullable: true),
                    KelasId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    TempatLahir = table.Column<string>(type: "TEXT", nullable: true),
                    TanggalLahir = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    AsalSekolah = table.Column<string>(type: "TEXT", nullable: true),
                    AlamatJalan = table.Column<string>(type: "TEXT", nullable: true),
                    AlamatRt = table.Column<string>(type: "TEXT", nullable: true),
                    AlamatRw = table.Column<string>(type: "TEXT", nullable: true),
                    AlamatKelurahan = table.Column<string>(type: "TEXT", nullable: true),
                    AlamatKecamatan = table.Column<string>(type: "TEXT", nullable: true),
                    NamaAyah = table.Column<string>(type: "TEXT", nullable: true),
                    PekerjaanAyah = table.Column<string>(type: "TEXT", nullable: true),
                    NamaIbu = table.Column<string>(type: "TEXT", nullable: true),
                    PekerjaanIbu = table.Column<string>(type: "TEXT", nullable: true),
                    NoHandphone = table.Column<string>(type: "TEXT", nullable: true),
                    DokumenKk = table.Column<string>(type: "TEXT", nullable: true),
                    DokumenAkta = table.Column<string>(type: "TEXT", nullable: true),
                    DokumenKia = table.Column<string>(type: "TEXT", nullable: true),
                    DokumenIjazah = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Siswa", x => x.SiswaId);
                    table.ForeignKey(
                        name: "FK_Siswa_Kelas_KelasId",
                        column: x => x.KelasId,
                        principalTable: "Kelas",
                        principalColumn: "KelasId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Guru",
                columns: table => new
                {
                    GuruId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nama = table.Column<string>(type: "TEXT", nullable: false),
                    Nip = table.Column<string>(type: "TEXT", nullable: true),
                    NomorUrut = table.Column<int>(type: "INTEGER", nullable: true),
                    JenisKelamin = table.Column<string>(type: "TEXT", nullable: true),
                    Jabatan = table.Column<string>(type: "TEXT", nullable: false),
                    MataPelajaranCatatan = table.Column<string>(type: "TEXT", nullable: true),
                    JumlahJamMengajar = table.Column<short>(type: "INTEGER", nullable: true),
                    GelarTerakhir = table.Column<string>(type: "TEXT", nullable: true),
                    NoHandphone = table.Column<string>(type: "TEXT", nullable: true),
                    Alamat = table.Column<string>(type: "TEXT", nullable: true),
                    StatusAktif = table.Column<bool>(type: "INTEGER", nullable: false),
                    StatusKeluar = table.Column<string>(type: "TEXT", nullable: true),
                    TahunAjaranMulaiId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guru", x => x.GuruId);
                    table.ForeignKey(
                        name: "FK_Guru_TahunAjaran_TahunAjaranMulaiId",
                        column: x => x.TahunAjaranMulaiId,
                        principalTable: "TahunAjaran",
                        principalColumn: "TahunAjaranId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "JamPelajaran",
                columns: table => new
                {
                    JamPelajaranId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TahunAjaranId = table.Column<int>(type: "INTEGER", nullable: false),
                    Hari = table.Column<string>(type: "TEXT", nullable: false),
                    JamKe = table.Column<int>(type: "INTEGER", nullable: false),
                    JamMulai = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    JamSelesai = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    Jenis = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JamPelajaran", x => x.JamPelajaranId);
                    table.ForeignKey(
                        name: "FK_JamPelajaran_TahunAjaran_TahunAjaranId",
                        column: x => x.TahunAjaranId,
                        principalTable: "TahunAjaran",
                        principalColumn: "TahunAjaranId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KalenderAkademik",
                columns: table => new
                {
                    KalenderAkademikId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TahunAjaranId = table.Column<int>(type: "INTEGER", nullable: false),
                    Judul = table.Column<string>(type: "TEXT", nullable: false),
                    Kategori = table.Column<string>(type: "TEXT", nullable: true),
                    Warna = table.Column<string>(type: "TEXT", nullable: true),
                    TanggalMulai = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TanggalSelesai = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Waktu = table.Column<string>(type: "TEXT", nullable: true),
                    Sasaran = table.Column<string>(type: "TEXT", nullable: true),
                    IsLibur = table.Column<bool>(type: "INTEGER", nullable: false),
                    Keterangan = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KalenderAkademik", x => x.KalenderAkademikId);
                    table.ForeignKey(
                        name: "FK_KalenderAkademik_TahunAjaran_TahunAjaranId",
                        column: x => x.TahunAjaranId,
                        principalTable: "TahunAjaran",
                        principalColumn: "TahunAjaranId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KurikulumAlokasi",
                columns: table => new
                {
                    KurikulumAlokasiId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TahunAjaranId = table.Column<int>(type: "INTEGER", nullable: false),
                    TingkatKode = table.Column<string>(type: "TEXT", nullable: false),
                    MataPelajaranId = table.Column<int>(type: "INTEGER", nullable: false),
                    JpPerMinggu = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KurikulumAlokasi", x => x.KurikulumAlokasiId);
                    table.ForeignKey(
                        name: "FK_KurikulumAlokasi_MataPelajaran_MataPelajaranId",
                        column: x => x.MataPelajaranId,
                        principalTable: "MataPelajaran",
                        principalColumn: "MataPelajaranId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KurikulumAlokasi_TahunAjaran_TahunAjaranId",
                        column: x => x.TahunAjaranId,
                        principalTable: "TahunAjaran",
                        principalColumn: "TahunAjaranId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuthGroupUsers",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    AuthGroupId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthGroupUsers", x => new { x.UserId, x.AuthGroupId });
                    table.ForeignKey(
                        name: "FK_AuthGroupUsers_AuthGroups_AuthGroupId",
                        column: x => x.AuthGroupId,
                        principalTable: "AuthGroups",
                        principalColumn: "AuthGroupId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuthGroupUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalonSiswa",
                columns: table => new
                {
                    CalonSiswaId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nama = table.Column<string>(type: "TEXT", nullable: false),
                    JenisKelamin = table.Column<string>(type: "TEXT", nullable: false),
                    Nisn = table.Column<string>(type: "TEXT", nullable: true),
                    TempatLahir = table.Column<string>(type: "TEXT", nullable: true),
                    TanggalLahir = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    AsalSekolah = table.Column<string>(type: "TEXT", nullable: true),
                    TingkatDituju = table.Column<string>(type: "TEXT", nullable: true),
                    AlamatJalan = table.Column<string>(type: "TEXT", nullable: true),
                    AlamatRt = table.Column<string>(type: "TEXT", nullable: true),
                    AlamatRw = table.Column<string>(type: "TEXT", nullable: true),
                    AlamatKelurahan = table.Column<string>(type: "TEXT", nullable: true),
                    AlamatKecamatan = table.Column<string>(type: "TEXT", nullable: true),
                    NamaAyah = table.Column<string>(type: "TEXT", nullable: true),
                    PekerjaanAyah = table.Column<string>(type: "TEXT", nullable: true),
                    NamaIbu = table.Column<string>(type: "TEXT", nullable: true),
                    PekerjaanIbu = table.Column<string>(type: "TEXT", nullable: true),
                    NoHandphone = table.Column<string>(type: "TEXT", nullable: true),
                    DokumenKk = table.Column<string>(type: "TEXT", nullable: true),
                    DokumenAkta = table.Column<string>(type: "TEXT", nullable: true),
                    DokumenKia = table.Column<string>(type: "TEXT", nullable: true),
                    DokumenIjazah = table.Column<string>(type: "TEXT", nullable: true),
                    Catatan = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    SiswaId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalonSiswa", x => x.CalonSiswaId);
                    table.ForeignKey(
                        name: "FK_CalonSiswa_Siswa_SiswaId",
                        column: x => x.SiswaId,
                        principalTable: "Siswa",
                        principalColumn: "SiswaId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EkskulSiswa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EkskulId = table.Column<int>(type: "INTEGER", nullable: false),
                    SiswaId = table.Column<int>(type: "INTEGER", nullable: false),
                    TanggalDaftar = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkskulSiswa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EkskulSiswa_Ekskul_EkskulId",
                        column: x => x.EkskulId,
                        principalTable: "Ekskul",
                        principalColumn: "EkskulId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EkskulSiswa_Siswa_SiswaId",
                        column: x => x.SiswaId,
                        principalTable: "Siswa",
                        principalColumn: "SiswaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiwayatAkademik",
                columns: table => new
                {
                    RiwayatAkademikId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SiswaId = table.Column<int>(type: "INTEGER", nullable: false),
                    TahunAjaranId = table.Column<int>(type: "INTEGER", nullable: false),
                    KelasId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiwayatAkademik", x => x.RiwayatAkademikId);
                    table.ForeignKey(
                        name: "FK_RiwayatAkademik_Siswa_SiswaId",
                        column: x => x.SiswaId,
                        principalTable: "Siswa",
                        principalColumn: "SiswaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RiwayatAkademik_TahunAjaran_TahunAjaranId",
                        column: x => x.TahunAjaranId,
                        principalTable: "TahunAjaran",
                        principalColumn: "TahunAjaranId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuruMataPelajaran",
                columns: table => new
                {
                    GuruMataPelajaranId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuruId = table.Column<int>(type: "INTEGER", nullable: false),
                    MataPelajaranId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tingkat = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuruMataPelajaran", x => x.GuruMataPelajaranId);
                    table.ForeignKey(
                        name: "FK_GuruMataPelajaran_Guru_GuruId",
                        column: x => x.GuruId,
                        principalTable: "Guru",
                        principalColumn: "GuruId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuruMataPelajaran_MataPelajaran_MataPelajaranId",
                        column: x => x.MataPelajaranId,
                        principalTable: "MataPelajaran",
                        principalColumn: "MataPelajaranId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JadwalPiket",
                columns: table => new
                {
                    JadwalPiketId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TahunAjaranId = table.Column<int>(type: "INTEGER", nullable: false),
                    Semester = table.Column<string>(type: "TEXT", nullable: false),
                    Hari = table.Column<string>(type: "TEXT", nullable: false),
                    GuruId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JadwalPiket", x => x.JadwalPiketId);
                    table.ForeignKey(
                        name: "FK_JadwalPiket_Guru_GuruId",
                        column: x => x.GuruId,
                        principalTable: "Guru",
                        principalColumn: "GuruId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JadwalPiket_TahunAjaran_TahunAjaranId",
                        column: x => x.TahunAjaranId,
                        principalTable: "TahunAjaran",
                        principalColumn: "TahunAjaranId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KepalaSekolah",
                columns: table => new
                {
                    KepalaSekolahId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuruId = table.Column<int>(type: "INTEGER", nullable: false),
                    TahunAjaranId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KepalaSekolah", x => x.KepalaSekolahId);
                    table.ForeignKey(
                        name: "FK_KepalaSekolah_Guru_GuruId",
                        column: x => x.GuruId,
                        principalTable: "Guru",
                        principalColumn: "GuruId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KepalaSekolah_TahunAjaran_TahunAjaranId",
                        column: x => x.TahunAjaranId,
                        principalTable: "TahunAjaran",
                        principalColumn: "TahunAjaranId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WaliKelas",
                columns: table => new
                {
                    WaliKelasId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KelasId = table.Column<int>(type: "INTEGER", nullable: false),
                    GuruId = table.Column<int>(type: "INTEGER", nullable: false),
                    TahunAjaranId = table.Column<int>(type: "INTEGER", nullable: false),
                    Urutan = table.Column<byte>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaliKelas", x => x.WaliKelasId);
                    table.ForeignKey(
                        name: "FK_WaliKelas_Guru_GuruId",
                        column: x => x.GuruId,
                        principalTable: "Guru",
                        principalColumn: "GuruId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WaliKelas_Kelas_KelasId",
                        column: x => x.KelasId,
                        principalTable: "Kelas",
                        principalColumn: "KelasId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WaliKelas_TahunAjaran_TahunAjaranId",
                        column: x => x.TahunAjaranId,
                        principalTable: "TahunAjaran",
                        principalColumn: "TahunAjaranId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JadwalPelajaran",
                columns: table => new
                {
                    JadwalPelajaranId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TahunAjaranId = table.Column<int>(type: "INTEGER", nullable: false),
                    Semester = table.Column<string>(type: "TEXT", nullable: false),
                    KelasId = table.Column<int>(type: "INTEGER", nullable: false),
                    JamPelajaranId = table.Column<int>(type: "INTEGER", nullable: false),
                    MataPelajaranId = table.Column<int>(type: "INTEGER", nullable: false),
                    GuruId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JadwalPelajaran", x => x.JadwalPelajaranId);
                    table.ForeignKey(
                        name: "FK_JadwalPelajaran_Guru_GuruId",
                        column: x => x.GuruId,
                        principalTable: "Guru",
                        principalColumn: "GuruId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JadwalPelajaran_JamPelajaran_JamPelajaranId",
                        column: x => x.JamPelajaranId,
                        principalTable: "JamPelajaran",
                        principalColumn: "JamPelajaranId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JadwalPelajaran_Kelas_KelasId",
                        column: x => x.KelasId,
                        principalTable: "Kelas",
                        principalColumn: "KelasId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JadwalPelajaran_MataPelajaran_MataPelajaranId",
                        column: x => x.MataPelajaranId,
                        principalTable: "MataPelajaran",
                        principalColumn: "MataPelajaranId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JadwalPelajaran_TahunAjaran_TahunAjaranId",
                        column: x => x.TahunAjaranId,
                        principalTable: "TahunAjaran",
                        principalColumn: "TahunAjaranId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JamPelajaranTingkat",
                columns: table => new
                {
                    JamPelajaranTingkatId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JamPelajaranId = table.Column<int>(type: "INTEGER", nullable: false),
                    TingkatKode = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JamPelajaranTingkat", x => x.JamPelajaranTingkatId);
                    table.ForeignKey(
                        name: "FK_JamPelajaranTingkat_JamPelajaran_JamPelajaranId",
                        column: x => x.JamPelajaranId,
                        principalTable: "JamPelajaran",
                        principalColumn: "JamPelajaranId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthGroups_Name",
                table: "AuthGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthGroupUsers_AuthGroupId",
                table: "AuthGroupUsers",
                column: "AuthGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CalonSiswa_SiswaId",
                table: "CalonSiswa",
                column: "SiswaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalonSiswa_Status",
                table: "CalonSiswa",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EkskulSiswa_EkskulId_SiswaId",
                table: "EkskulSiswa",
                columns: new[] { "EkskulId", "SiswaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EkskulSiswa_SiswaId",
                table: "EkskulSiswa",
                column: "SiswaId");

            migrationBuilder.CreateIndex(
                name: "IX_Guru_Nip",
                table: "Guru",
                column: "Nip");

            migrationBuilder.CreateIndex(
                name: "IX_Guru_NomorUrut",
                table: "Guru",
                column: "NomorUrut",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guru_TahunAjaranMulaiId",
                table: "Guru",
                column: "TahunAjaranMulaiId");

            migrationBuilder.CreateIndex(
                name: "IX_GuruMataPelajaran_GuruId_MataPelajaranId_Tingkat",
                table: "GuruMataPelajaran",
                columns: new[] { "GuruId", "MataPelajaranId", "Tingkat" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuruMataPelajaran_MataPelajaranId",
                table: "GuruMataPelajaran",
                column: "MataPelajaranId");

            migrationBuilder.CreateIndex(
                name: "IX_JadwalPelajaran_GuruId_JamPelajaranId_Semester",
                table: "JadwalPelajaran",
                columns: new[] { "GuruId", "JamPelajaranId", "Semester" });

            migrationBuilder.CreateIndex(
                name: "IX_JadwalPelajaran_JamPelajaranId",
                table: "JadwalPelajaran",
                column: "JamPelajaranId");

            migrationBuilder.CreateIndex(
                name: "IX_JadwalPelajaran_KelasId",
                table: "JadwalPelajaran",
                column: "KelasId");

            migrationBuilder.CreateIndex(
                name: "IX_JadwalPelajaran_MataPelajaranId",
                table: "JadwalPelajaran",
                column: "MataPelajaranId");

            migrationBuilder.CreateIndex(
                name: "IX_JadwalPelajaran_TahunAjaranId_Semester_KelasId_JamPelajaranId",
                table: "JadwalPelajaran",
                columns: new[] { "TahunAjaranId", "Semester", "KelasId", "JamPelajaranId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JadwalPiket_GuruId",
                table: "JadwalPiket",
                column: "GuruId");

            migrationBuilder.CreateIndex(
                name: "IX_JadwalPiket_TahunAjaranId_Semester_Hari_GuruId",
                table: "JadwalPiket",
                columns: new[] { "TahunAjaranId", "Semester", "Hari", "GuruId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JamPelajaran_TahunAjaranId_Hari_JamKe",
                table: "JamPelajaran",
                columns: new[] { "TahunAjaranId", "Hari", "JamKe" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JamPelajaranTingkat_JamPelajaranId_TingkatKode",
                table: "JamPelajaranTingkat",
                columns: new[] { "JamPelajaranId", "TingkatKode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KalenderAkademik_TahunAjaranId_TanggalMulai",
                table: "KalenderAkademik",
                columns: new[] { "TahunAjaranId", "TanggalMulai" });

            migrationBuilder.CreateIndex(
                name: "IX_Kelas_Tingkat_NamaKelas",
                table: "Kelas",
                columns: new[] { "Tingkat", "NamaKelas" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KepalaSekolah_GuruId_TahunAjaranId",
                table: "KepalaSekolah",
                columns: new[] { "GuruId", "TahunAjaranId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KepalaSekolah_TahunAjaranId",
                table: "KepalaSekolah",
                column: "TahunAjaranId");

            migrationBuilder.CreateIndex(
                name: "IX_KurikulumAlokasi_MataPelajaranId",
                table: "KurikulumAlokasi",
                column: "MataPelajaranId");

            migrationBuilder.CreateIndex(
                name: "IX_KurikulumAlokasi_TahunAjaranId_TingkatKode_MataPelajaranId",
                table: "KurikulumAlokasi",
                columns: new[] { "TahunAjaranId", "TingkatKode", "MataPelajaranId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MataPelajaran_Kode",
                table: "MataPelajaran",
                column: "Kode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MataPelajaran_Nama",
                table: "MataPelajaran",
                column: "Nama",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiwayatAkademik_KelasId",
                table: "RiwayatAkademik",
                column: "KelasId");

            migrationBuilder.CreateIndex(
                name: "IX_RiwayatAkademik_SiswaId_TahunAjaranId",
                table: "RiwayatAkademik",
                columns: new[] { "SiswaId", "TahunAjaranId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiwayatAkademik_TahunAjaranId",
                table: "RiwayatAkademik",
                column: "TahunAjaranId");

            migrationBuilder.CreateIndex(
                name: "IX_Siswa_KelasId",
                table: "Siswa",
                column: "KelasId");

            migrationBuilder.CreateIndex(
                name: "IX_Siswa_Nis",
                table: "Siswa",
                column: "Nis",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Siswa_Status",
                table: "Siswa",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TahunAjaran_Nama",
                table: "TahunAjaran",
                column: "Nama",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tingkat_Kode",
                table: "Tingkat",
                column: "Kode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaliKelas_GuruId_TahunAjaranId",
                table: "WaliKelas",
                columns: new[] { "GuruId", "TahunAjaranId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaliKelas_KelasId_TahunAjaranId_Urutan",
                table: "WaliKelas",
                columns: new[] { "KelasId", "TahunAjaranId", "Urutan" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaliKelas_TahunAjaranId",
                table: "WaliKelas",
                column: "TahunAjaranId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthGroupUsers");

            migrationBuilder.DropTable(
                name: "CalonSiswa");

            migrationBuilder.DropTable(
                name: "EkskulSiswa");

            migrationBuilder.DropTable(
                name: "GuruMataPelajaran");

            migrationBuilder.DropTable(
                name: "JadwalPelajaran");

            migrationBuilder.DropTable(
                name: "JadwalPiket");

            migrationBuilder.DropTable(
                name: "JamPelajaranTingkat");

            migrationBuilder.DropTable(
                name: "KalenderAkademik");

            migrationBuilder.DropTable(
                name: "KepalaSekolah");

            migrationBuilder.DropTable(
                name: "KurikulumAlokasi");

            migrationBuilder.DropTable(
                name: "RiwayatAkademik");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "Tingkat");

            migrationBuilder.DropTable(
                name: "WaliKelas");

            migrationBuilder.DropTable(
                name: "AuthGroups");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Ekskul");

            migrationBuilder.DropTable(
                name: "JamPelajaran");

            migrationBuilder.DropTable(
                name: "MataPelajaran");

            migrationBuilder.DropTable(
                name: "Siswa");

            migrationBuilder.DropTable(
                name: "Guru");

            migrationBuilder.DropTable(
                name: "Kelas");

            migrationBuilder.DropTable(
                name: "TahunAjaran");
        }
    }
}
