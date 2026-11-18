using System;
using BlazorWebApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260430120000_Add_WorkshopWizardSession")]
    public partial class Add_WorkshopWizardSession : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    table.ForeignKey(
                        name: "FK_WorkshopWizardSessions_PromptWorkshopSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "PromptWorkshopSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Filtered unique index: one wizard row per Workshop session, plus exactly one
            // unbound row (SessionId IS NULL) for the no-session-yet slot.
            migrationBuilder.CreateIndex(
                name: "IX_WorkshopWizardSessions_SessionId",
                table: "WorkshopWizardSessions",
                column: "SessionId",
                unique: true,
                filter: "\"SessionId\" IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkshopWizardSessions");
        }
    }
}
