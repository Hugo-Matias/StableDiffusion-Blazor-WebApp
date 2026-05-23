using BlazorWebApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260522120000_Add_Resource_HuggingFace_Fields")]
    public partial class Add_Resource_HuggingFace_Fields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HuggingFaceFilePath",
                table: "Resources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HuggingFaceModelId",
                table: "Resources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HuggingFaceRevision",
                table: "Resources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HuggingFaceSha",
                table: "Resources",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HuggingFaceFilePath",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "HuggingFaceModelId",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "HuggingFaceRevision",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "HuggingFaceSha",
                table: "Resources");
        }
    }
}
