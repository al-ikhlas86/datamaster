using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMaster.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPenilaianSikap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PenilaianSikap",
                columns: table => new
                {
                    PenilaianSikapId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SiswaId = table.Column<int>(type: "INTEGER", nullable: false),
                    TahunAjaranId = table.Column<int>(type: "INTEGER", nullable: false),
                    Semester = table.Column<string>(type: "TEXT", nullable: false),
                    Grade = table.Column<string>(type: "TEXT", nullable: false),
                    Catatan = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PenilaianSikap", x => x.PenilaianSikapId);
                    table.ForeignKey(
                        name: "FK_PenilaianSikap_Siswa_SiswaId",
                        column: x => x.SiswaId,
                        principalTable: "Siswa",
                        principalColumn: "SiswaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PenilaianSikap_TahunAjaran_TahunAjaranId",
                        column: x => x.TahunAjaranId,
                        principalTable: "TahunAjaran",
                        principalColumn: "TahunAjaranId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PenilaianSikap_SiswaId_TahunAjaranId_Semester",
                table: "PenilaianSikap",
                columns: new[] { "SiswaId", "TahunAjaranId", "Semester" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PenilaianSikap_TahunAjaranId",
                table: "PenilaianSikap",
                column: "TahunAjaranId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PenilaianSikap");
        }
    }
}
