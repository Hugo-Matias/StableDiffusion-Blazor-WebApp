using System;
using BlazorWebApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260424000000_Add_SavedDanbooruMedia")]
    public partial class Add_SavedDanbooruMedia : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedDanbooruMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DanbooruPostId = table.Column<int>(type: "INTEGER", nullable: false),
                    Rating = table.Column<string>(type: "TEXT", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    Extension = table.Column<string>(type: "TEXT", nullable: false),
                    IsVideo = table.Column<bool>(type: "INTEGER", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    SampleUrl = table.Column<string>(type: "TEXT", nullable: false),
                    PreviewUrl = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TagsBundle = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedDanbooruMedia", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedDanbooruMedia_DanbooruPostId",
                table: "SavedDanbooruMedia",
                column: "DanbooruPostId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedDanbooruMedia");
        }
    }
}
