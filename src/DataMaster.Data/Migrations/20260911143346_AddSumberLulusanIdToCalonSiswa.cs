using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMaster.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSumberLulusanIdToCalonSiswa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SumberLulusanId",
                table: "CalonSiswa",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SumberLulusanId",
                table: "CalonSiswa");
        }
    }
}
