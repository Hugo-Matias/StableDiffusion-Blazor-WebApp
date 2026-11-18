using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    public partial class AddWorkflowState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Img2ImgParameters",
                table: "States");

            migrationBuilder.DropColumn(
                name: "Img2VidParameters",
                table: "States");

            migrationBuilder.DropColumn(
                name: "Txt2ImgParameters",
                table: "States");

            migrationBuilder.DropColumn(
                name: "UpscaleParameters",
                table: "States");

            migrationBuilder.CreateTable(
                name: "WorkflowStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Parameters = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStates_WorkflowId",
                table: "WorkflowStates",
                column: "WorkflowId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowStates");

            migrationBuilder.AddColumn<string>(
                name: "Img2ImgParameters",
                table: "States",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Img2VidParameters",
                table: "States",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Txt2ImgParameters",
                table: "States",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpscaleParameters",
                table: "States",
                type: "TEXT",
                nullable: true);
        }
    }
}
