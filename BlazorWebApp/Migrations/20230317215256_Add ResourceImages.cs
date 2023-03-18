using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    public partial class AddResourceImages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResourceImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CivitaiModelId = table.Column<int>(type: "INTEGER", nullable: false),
                    CivitaiModelVersionID = table.Column<int>(type: "INTEGER", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Nsfw = table.Column<bool>(type: "INTEGER", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: true),
                    GenerationProcess = table.Column<string>(type: "TEXT", nullable: false),
                    Hash = table.Column<string>(type: "TEXT", nullable: false),
                    Model = table.Column<string>(type: "TEXT", nullable: false),
                    ModelHash = table.Column<string>(type: "TEXT", nullable: false),
                    Prompt = table.Column<string>(type: "TEXT", nullable: false),
                    NegativePrompt = table.Column<string>(type: "TEXT", nullable: false),
                    Seed = table.Column<long>(type: "INTEGER", nullable: false),
                    Sampler = table.Column<string>(type: "TEXT", nullable: false),
                    Steps = table.Column<int>(type: "INTEGER", nullable: false),
                    CfgScale = table.Column<float>(type: "REAL", nullable: false),
                    HiresUpscale = table.Column<string>(type: "TEXT", nullable: false),
                    HiresUpscaler = table.Column<string>(type: "TEXT", nullable: false),
                    HiresSteps = table.Column<string>(type: "TEXT", nullable: false),
                    DenoisingStrength = table.Column<string>(type: "TEXT", nullable: false),
                    FaceRestoration = table.Column<string>(type: "TEXT", nullable: false),
                    ClipSkip = table.Column<string>(type: "TEXT", nullable: false),
                    ENSD = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceImages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceImages_Hash",
                table: "ResourceImages",
                column: "Hash",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResourceImages");
        }
    }
}
