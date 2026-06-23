using BlazorWebApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260511214500_Add_Cleanup_Group_Explanations")]
    public partial class Add_Cleanup_Group_Explanations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CleanupGroupExplanations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    RepresentativeImageId = table.Column<int>(type: "INTEGER", nullable: true),
                    ModelName = table.Column<string>(type: "TEXT", nullable: false),
                    Style = table.Column<string>(type: "TEXT", nullable: false),
                    Caption = table.Column<string>(type: "TEXT", nullable: false),
                    Explanation = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleanupGroupExplanations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CleanupGroupExplanations_CleanupGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "CleanupGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CleanupGroupExplanations_Images_RepresentativeImageId",
                        column: x => x.RepresentativeImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CleanupGroupExplanations_GroupId",
                table: "CleanupGroupExplanations",
                column: "GroupId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CleanupGroupExplanations_RepresentativeImageId",
                table: "CleanupGroupExplanations",
                column: "RepresentativeImageId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CleanupGroupExplanations");
        }
    }
}