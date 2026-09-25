using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMaster.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRencanaKenaikan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RencanaKenaikan",
                columns: table => new
                {
                    RencanaKenaikanId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TahunAjaranTujuanId = table.Column<int>(type: "INTEGER", nullable: false),
                    SiswaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Lulus = table.Column<bool>(type: "INTEGER", nullable: false),
                    KelasTujuanId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RencanaKenaikan", x => x.RencanaKenaikanId);
                    table.ForeignKey(
                        name: "FK_RencanaKenaikan_Siswa_SiswaId",
                        column: x => x.SiswaId,
                        principalTable: "Siswa",
                        principalColumn: "SiswaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RencanaKenaikan_TahunAjaran_TahunAjaranTujuanId",
                        column: x => x.TahunAjaranTujuanId,
                        principalTable: "TahunAjaran",
                        principalColumn: "TahunAjaranId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RencanaKenaikan_SiswaId_TahunAjaranTujuanId",
                table: "RencanaKenaikan",
                columns: new[] { "SiswaId", "TahunAjaranTujuanId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RencanaKenaikan_TahunAjaranTujuanId",
                table: "RencanaKenaikan",
                column: "TahunAjaranTujuanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RencanaKenaikan");
        }
    }
}
