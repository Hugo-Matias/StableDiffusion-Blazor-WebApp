using BlazorWebApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260521120000_Add_Resource_Version_Metadata")]
    public partial class Add_Resource_Version_Metadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VersionDescription",
                table: "Resources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VersionName",
                table: "Resources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("UPDATE Resources SET VersionDescription = Description WHERE VersionDescription IS NULL AND Description IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VersionDescription",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "VersionName",
                table: "Resources");
        }
    }
}