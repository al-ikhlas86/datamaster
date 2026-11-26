using DataMaster.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMaster.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DataMasterDbContext))]
    [Migration("20261127100000_AddPsbOnlineToCalonSiswa")]
    public partial class AddPsbOnlineToCalonSiswa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "PsbOnlineId", table: "CalonSiswa", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<string>(name: "PsbNomor", table: "CalonSiswa", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<string>(name: "PsbStatus", table: "CalonSiswa", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<string>(name: "PsbGelombang", table: "CalonSiswa", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<string>(name: "PsbJson", table: "CalonSiswa", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "PsbDisinkronAt", table: "CalonSiswa", type: "TEXT", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_CalonSiswa_PsbOnlineId", table: "CalonSiswa", column: "PsbOnlineId", unique: true, filter: "PsbOnlineId IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_CalonSiswa_PsbOnlineId", table: "CalonSiswa");
            migrationBuilder.DropColumn(name: "PsbDisinkronAt", table: "CalonSiswa");
            migrationBuilder.DropColumn(name: "PsbJson", table: "CalonSiswa");
            migrationBuilder.DropColumn(name: "PsbGelombang", table: "CalonSiswa");
            migrationBuilder.DropColumn(name: "PsbStatus", table: "CalonSiswa");
            migrationBuilder.DropColumn(name: "PsbNomor", table: "CalonSiswa");
            migrationBuilder.DropColumn(name: "PsbOnlineId", table: "CalonSiswa");
        }
    }
}
