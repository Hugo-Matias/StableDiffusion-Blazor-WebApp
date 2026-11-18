using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    public partial class AddImg2VidParametersToState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Img2VidParameters",
                table: "States",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Img2VidParameters",
                table: "States");
        }
    }
}
