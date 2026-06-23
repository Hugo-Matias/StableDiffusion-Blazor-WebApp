using System;
using BlazorWebApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260426162700_Add_PromptWorkshop")]
    public partial class Add_PromptWorkshop : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PromptWorkshopSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CurrentNodeId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptWorkshopSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromptWorkshopNodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentId = table.Column<int>(type: "INTEGER", nullable: true),
                    GenerationNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    PromptText = table.Column<string>(type: "TEXT", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Instruction = table.Column<string>(type: "TEXT", nullable: true),
                    ModelUsed = table.Column<string>(type: "TEXT", nullable: true),
                    ImageIdsJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptWorkshopNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromptWorkshopNodes_PromptWorkshopSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "PromptWorkshopSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PromptWorkshopNodes_PromptWorkshopNodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "PromptWorkshopNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromptWorkshopNodes_ParentId",
                table: "PromptWorkshopNodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptWorkshopNodes_SessionId",
                table: "PromptWorkshopNodes",
                column: "SessionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromptWorkshopNodes");

            migrationBuilder.DropTable(
                name: "PromptWorkshopSessions");
        }
    }
}
