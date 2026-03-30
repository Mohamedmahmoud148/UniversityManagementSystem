using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentFileAndEnrollmentUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnrollmentUploads",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    UploadedByAdminId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedCount = table.Column<int>(type: "integer", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnrollmentUploads_SystemUsers_UploadedByAdminId",
                        column: x => x.UploadedByAdminId,
                        principalTable: "SystemUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentFiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByStudentId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    ExtractedText = table.Column<string>(type: "text", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentFiles_Students_UploadedByStudentId",
                        column: x => x.UploadedByStudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentUploads_UploadedByAdminId",
                table: "EnrollmentUploads",
                column: "UploadedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentFiles_UploadedByStudentId",
                table: "StudentFiles",
                column: "UploadedByStudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnrollmentUploads");

            migrationBuilder.DropTable(
                name: "StudentFiles");
        }
    }
}
