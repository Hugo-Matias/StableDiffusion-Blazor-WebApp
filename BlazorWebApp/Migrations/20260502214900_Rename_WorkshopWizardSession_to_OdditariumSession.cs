using System;
using BlazorWebApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260502214900_Rename_WorkshopWizardSession_to_OdditariumSession")]
    public partial class Rename_WorkshopWizardSession_to_OdditariumSession : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite doesn't support DropForeignKey or RenameTable, so drop and recreate.
            migrationBuilder.DropTable(
                name: "WorkshopWizardSessions");

            migrationBuilder.CreateTable(
                name: "OdditariumSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: true),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OdditariumSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OdditariumSessions_SessionId",
                table: "OdditariumSessions",
                column: "SessionId",
                unique: true,
                filter: "\"SessionId\" IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OdditariumSessions");

            migrationBuilder.CreateTable(
                name: "WorkshopWizardSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: true),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkshopWizardSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkshopWizardSessions_SessionId",
                table: "WorkshopWizardSessions",
                column: "SessionId",
                unique: true,
                filter: "\"SessionId\" IS NOT NULL");
        }
    }
}
