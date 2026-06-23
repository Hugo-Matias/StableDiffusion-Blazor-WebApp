using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    public partial class AddSchedulerandModeltoImage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ResourceId",
                table: "Images",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scheduler",
                table: "Images",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Images_ResourceId",
                table: "Images",
                column: "ResourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Resources_ResourceId",
                table: "Images",
                column: "ResourceId",
                principalTable: "Resources",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Images_Resources_ResourceId",
                table: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Images_ResourceId",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "ResourceId",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "Scheduler",
                table: "Images");
        }
    }
}
