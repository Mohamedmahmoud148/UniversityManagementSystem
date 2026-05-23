using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProctoringAndAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExamProctoringLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    ExamSubmissionId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    StudentId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    ExamId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    TabSwitchCount = table.Column<int>(type: "integer", nullable: false),
                    FullscreenExitCount = table.Column<int>(type: "integer", nullable: false),
                    SuspiciousActivityCount = table.Column<int>(type: "integer", nullable: false),
                    EventsJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DoctorNote = table.Column<string>(type: "text", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamProctoringLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamProctoringLogs_ExamSubmissions_ExamSubmissionId",
                        column: x => x.ExamSubmissionId,
                        principalTable: "ExamSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamProctoringLogs_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamProctoringLogs_ExamId",
                table: "ExamProctoringLogs",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamProctoringLogs_StudentId",
                table: "ExamProctoringLogs",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamProctoringLogs_SubmissionId",
                table: "ExamProctoringLogs",
                column: "ExamSubmissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamProctoringLogs");
        }
    }
}
