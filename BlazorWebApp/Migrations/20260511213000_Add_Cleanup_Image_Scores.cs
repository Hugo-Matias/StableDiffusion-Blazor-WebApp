using BlazorWebApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260511213000_Add_Cleanup_Image_Scores")]
    public partial class Add_Cleanup_Image_Scores : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CleanupImageScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImageId = table.Column<int>(type: "INTEGER", nullable: false),
                    ModelKey = table.Column<string>(type: "TEXT", nullable: false),
                    ModelHash = table.Column<string>(type: "TEXT", nullable: true),
                    ScoreName = table.Column<string>(type: "TEXT", nullable: false),
                    RuntimeProvider = table.Column<string>(type: "TEXT", nullable: true),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    MinScore = table.Column<double>(type: "REAL", nullable: true),
                    MaxScore = table.Column<double>(type: "REAL", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    IndexedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleanupImageScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CleanupImageScores_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CleanupImageScores_ImageId",
                table: "CleanupImageScores",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_CleanupImageScores_ImageId_ModelKey_ModelHash_ScoreName",
                table: "CleanupImageScores",
                columns: new[] { "ImageId", "ModelKey", "ModelHash", "ScoreName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CleanupImageScores_ScoreName",
                table: "CleanupImageScores",
                column: "ScoreName");

            migrationBuilder.CreateIndex(
                name: "IX_CleanupImageScores_Status",
                table: "CleanupImageScores",
                column: "Status");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CleanupImageScores");
        }
    }
}