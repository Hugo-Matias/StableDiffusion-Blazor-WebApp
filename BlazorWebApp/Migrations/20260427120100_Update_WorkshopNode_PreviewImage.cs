using BlazorWebApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260427120100_Update_WorkshopNode_PreviewImage")]
    public partial class Update_WorkshopNode_PreviewImage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Note: ImageIdsJson is intentionally NOT dropped. SQLite's DropColumn
            // forces a full table rebuild, which fails on PromptWorkshopNodes due to
            // its self-referencing ParentId FK. The column is nullable and orphaned in
            // code, so leaving it in place is harmless (see
            // Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md).
            migrationBuilder.AddColumn<int>(
                name: "PreviewImageId",
                table: "PromptWorkshopNodes",
                type: "INTEGER",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviewImageId",
                table: "PromptWorkshopNodes");
        }
    }
}
