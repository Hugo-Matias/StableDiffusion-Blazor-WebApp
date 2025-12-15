using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    public partial class AddWildcardTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WildcardCollections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsageCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WildcardCollections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WildcardEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CollectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Weight = table.Column<float>(type: "REAL", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WildcardEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WildcardEntries_WildcardCollections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "WildcardCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WildcardCollections_Category",
                table: "WildcardCollections",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_WildcardCollections_Name",
                table: "WildcardCollections",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_WildcardEntries_CollectionId",
                table: "WildcardEntries",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WildcardEntries_SortOrder",
                table: "WildcardEntries",
                column: "SortOrder");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WildcardEntries");

            migrationBuilder.DropTable(
                name: "WildcardCollections");
        }
    }
}
