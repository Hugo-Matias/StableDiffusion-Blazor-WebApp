using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    public partial class AddResourcesSubTypes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Resources_ResourceTypes_SubTypeId",
                table: "Resources");

            migrationBuilder.CreateTable(
                name: "ResourceSubTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceSubTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceSubTypes_Name",
                table: "ResourceSubTypes",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Resources_ResourceSubTypes_SubTypeId",
                table: "Resources",
                column: "SubTypeId",
                principalTable: "ResourceSubTypes",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Resources_ResourceSubTypes_SubTypeId",
                table: "Resources");

            migrationBuilder.DropTable(
                name: "ResourceSubTypes");

            migrationBuilder.AddForeignKey(
                name: "FK_Resources_ResourceTypes_SubTypeId",
                table: "Resources",
                column: "SubTypeId",
                principalTable: "ResourceTypes",
                principalColumn: "Id");
        }
    }
}
