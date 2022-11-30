using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    public partial class AddFKImageMode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Images_ModeId",
                table: "Images",
                column: "ModeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Modes_ModeId",
                table: "Images",
                column: "ModeId",
                principalTable: "Modes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Images_Modes_ModeId",
                table: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Images_ModeId",
                table: "Images");
        }
    }
}
