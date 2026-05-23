using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRagPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaterialChunks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    MaterialId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<string>(type: "text", nullable: false),
                    TokenCount = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialChunks_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RagSearchLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    StudentId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Query = table.Column<string>(type: "text", nullable: false),
                    RetrievedChunkIds = table.Column<string>(type: "text", nullable: false),
                    ResponseSummary = table.Column<string>(type: "text", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RagSearchLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialChunks_MaterialId",
                table: "MaterialChunks",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_RagSearchLogs_CreatedAt",
                table: "RagSearchLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RagSearchLogs_StudentId",
                table: "RagSearchLogs",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterialChunks");

            migrationBuilder.DropTable(
                name: "RagSearchLogs");
        }
    }
}
