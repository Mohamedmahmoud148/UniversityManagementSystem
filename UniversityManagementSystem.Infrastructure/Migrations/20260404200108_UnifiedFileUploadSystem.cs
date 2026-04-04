using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UnifiedFileUploadSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StoredPath",
                table: "UploadedFiles");

            migrationBuilder.AddColumn<string>(
                name: "FileId",
                table: "StudentFiles",
                type: "character varying(26)",
                maxLength: 26,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileId",
                table: "Materials",
                type: "character varying(26)",
                maxLength: 26,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentFiles_FileId",
                table: "StudentFiles",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_FileId",
                table: "Materials",
                column: "FileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_UploadedFiles_FileId",
                table: "Materials",
                column: "FileId",
                principalTable: "UploadedFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentFiles_UploadedFiles_FileId",
                table: "StudentFiles",
                column: "FileId",
                principalTable: "UploadedFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Materials_UploadedFiles_FileId",
                table: "Materials");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentFiles_UploadedFiles_FileId",
                table: "StudentFiles");

            migrationBuilder.DropIndex(
                name: "IX_StudentFiles_FileId",
                table: "StudentFiles");

            migrationBuilder.DropIndex(
                name: "IX_Materials_FileId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "FileId",
                table: "StudentFiles");

            migrationBuilder.DropColumn(
                name: "FileId",
                table: "Materials");

            migrationBuilder.AddColumn<string>(
                name: "StoredPath",
                table: "UploadedFiles",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
