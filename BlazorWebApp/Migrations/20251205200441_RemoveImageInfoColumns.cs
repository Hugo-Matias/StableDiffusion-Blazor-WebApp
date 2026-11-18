using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    public partial class RemoveImageInfoColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Info",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "InfoPath",
                table: "Images");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Info",
                table: "Images",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InfoPath",
                table: "Images",
                type: "TEXT",
                nullable: true);
        }
    }
}
