using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMaster.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNoHandphoneKeduaToSiswa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NoHandphoneKedua",
                table: "Siswa",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NoHandphoneKedua",
                table: "Siswa");
        }
    }
}
