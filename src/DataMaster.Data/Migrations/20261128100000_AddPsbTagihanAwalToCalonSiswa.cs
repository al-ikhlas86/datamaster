using DataMaster.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMaster.Data.Migrations
{
    /// <summary>
    /// Tagihan awal calon siswa PSB dari Keuangan (2026-11-28): status + total + dibayar. Ditulis tangan (snapshot disunting selaras);
    /// kecocokannya dijaga tes HasPendingModelChanges.
    /// </summary>
    [DbContext(typeof(DataMasterDbContext))]
    [Migration("20261128100000_AddPsbTagihanAwalToCalonSiswa")]
    public partial class AddPsbTagihanAwalToCalonSiswa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "PsbTagihanStatus", table: "CalonSiswa", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "PsbTagihanTotal", table: "CalonSiswa", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "PsbTagihanDibayar", table: "CalonSiswa", type: "TEXT", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PsbTagihanDibayar", table: "CalonSiswa");
            migrationBuilder.DropColumn(name: "PsbTagihanTotal", table: "CalonSiswa");
            migrationBuilder.DropColumn(name: "PsbTagihanStatus", table: "CalonSiswa");
        }
    }
}
