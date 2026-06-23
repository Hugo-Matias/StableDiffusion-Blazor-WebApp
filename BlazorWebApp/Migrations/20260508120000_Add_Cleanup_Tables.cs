using BlazorWebApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260508120000_Add_Cleanup_Tables")]
    public partial class Add_Cleanup_Tables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CleanupGroupRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Strategy = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: true),
                    ScopeKey = table.Column<string>(type: "TEXT", nullable: true),
                    ConfigurationJson = table.Column<string>(type: "TEXT", nullable: true),
                    SummaryJson = table.Column<string>(type: "TEXT", nullable: true),
                    TotalGroups = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalMembers = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleanupGroupRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CleanupImageEmbeddings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImageId = table.Column<int>(type: "INTEGER", nullable: false),
                    ModelKey = table.Column<string>(type: "TEXT", nullable: false),
                    ModelHash = table.Column<string>(type: "TEXT", nullable: true),
                    RuntimeProvider = table.Column<string>(type: "TEXT", nullable: true),
                    Dimensions = table.Column<int>(type: "INTEGER", nullable: false),
                    Vector = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    IndexedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleanupImageEmbeddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CleanupImageEmbeddings_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CleanupImageIndexes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImageId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    ModeId = table.Column<int>(type: "INTEGER", nullable: false),
                    ResourceId = table.Column<int>(type: "INTEGER", nullable: true),
                    WorkflowId = table.Column<string>(type: "TEXT", nullable: true),
                    ImagePath = table.Column<string>(type: "TEXT", nullable: false),
                    FileExists = table.Column<bool>(type: "INTEGER", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    FileLastWriteUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExactHash = table.Column<string>(type: "TEXT", nullable: true),
                    PerceptualHash = table.Column<string>(type: "TEXT", nullable: true),
                    PromptNormalized = table.Column<string>(type: "TEXT", nullable: true),
                    PromptFingerprint = table.Column<string>(type: "TEXT", nullable: true),
                    PromptTokenSignature = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    IndexVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    MetadataIndexedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HashIndexedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IndexedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleanupImageIndexes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CleanupImageIndexes_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CleanupGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<int>(type: "INTEGER", nullable: false),
                    GroupKey = table.Column<string>(type: "TEXT", nullable: false),
                    Strategy = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    RepresentativeImageId = table.Column<int>(type: "INTEGER", nullable: true),
                    MemberCount = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    MinSimilarity = table.Column<double>(type: "REAL", nullable: true),
                    MaxSimilarity = table.Column<double>(type: "REAL", nullable: true),
                    Confidence = table.Column<double>(type: "REAL", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleanupGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CleanupGroups_CleanupGroupRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "CleanupGroupRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CleanupGroups_Images_RepresentativeImageId",
                        column: x => x.RepresentativeImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CleanupGroupMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageId = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    SuggestedAction = table.Column<string>(type: "TEXT", nullable: false),
                    SimilarityScore = table.Column<double>(type: "REAL", nullable: true),
                    Distance = table.Column<double>(type: "REAL", nullable: true),
                    EstimatedBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleanupGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CleanupGroupMembers_CleanupGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "CleanupGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CleanupGroupMembers_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CleanupGroupMembers_GroupId",
                table: "CleanupGroupMembers",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CleanupGroupMembers_GroupId_ImageId",
                table: "CleanupGroupMembers",
                columns: new[] { "GroupId", "ImageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CleanupGroupMembers_ImageId",
                table: "CleanupGroupMembers",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_CleanupGroupRuns_ProjectId",
                table: "CleanupGroupRuns",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CleanupGroupRuns_Status",
                table: "CleanupGroupRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CleanupGroupRuns_Strategy",
                table: "CleanupGroupRuns",
                column: "Strategy");

            migrationBuilder.CreateIndex(
                name: "IX_CleanupGroups_RepresentativeImageId",
                table: "CleanupGroups",
                column: "RepresentativeImageId");

            migrationBuilder.CreateIndex(
                name: "IX_CleanupGroups_RunId",
                table: "CleanupGroups",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_CleanupGroups_RunId_GroupKey",
                table: "CleanupGroups",
                columns: new[] { "RunId", "GroupKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CleanupImageEmbeddings_ImageId",
                table: "CleanupImageEmbeddings",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_CleanupImageEmbeddings_ImageId_ModelKey_ModelHash",
                table: "CleanupImageEmbeddings",
                columns: new[] { "ImageId", "ModelKey", "ModelHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CleanupImageIndexes_ExactHash",
                table: "CleanupImageIndexes",
                column: "ExactHash");

            migrationBuilder.CreateIndex(
                name: "IX_CleanupImageIndexes_ImageId",
                table: "CleanupImageIndexes",
                column: "ImageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CleanupImageIndexes_PerceptualHash",
                table: "CleanupImageIndexes",
                column: "PerceptualHash");

            migrationBuilder.CreateIndex(
                name: "IX_CleanupImageIndexes_ProjectId",
                table: "CleanupImageIndexes",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CleanupImageIndexes_PromptFingerprint",
                table: "CleanupImageIndexes",
                column: "PromptFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_CleanupImageIndexes_Status",
                table: "CleanupImageIndexes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CleanupImageIndexes_WorkflowId",
                table: "CleanupImageIndexes",
                column: "WorkflowId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CleanupGroupMembers");
            migrationBuilder.DropTable(name: "CleanupImageEmbeddings");
            migrationBuilder.DropTable(name: "CleanupImageIndexes");
            migrationBuilder.DropTable(name: "CleanupGroups");
            migrationBuilder.DropTable(name: "CleanupGroupRuns");
        }
    }
}