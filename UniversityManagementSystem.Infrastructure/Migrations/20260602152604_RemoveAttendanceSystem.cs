using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAttendanceSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentAttendances");

            migrationBuilder.DropTable(
                name: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "AttendancePercent",
                table: "StudentIntelligenceSnapshots");

            migrationBuilder.DropColumn(
                name: "AttendanceScore",
                table: "StudentIntelligenceSnapshots");

            migrationBuilder.DropColumn(
                name: "AttendanceTrend",
                table: "StudentIntelligenceSnapshots");

            migrationBuilder.DropColumn(
                name: "AttendedSessions",
                table: "StudentIntelligenceSnapshots");

            migrationBuilder.DropColumn(
                name: "TotalSessions",
                table: "StudentIntelligenceSnapshots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AttendancePercent",
                table: "StudentIntelligenceSnapshots",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "AttendanceScore",
                table: "StudentIntelligenceSnapshots",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "AttendanceTrend",
                table: "StudentIntelligenceSnapshots",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "AttendedSessions",
                table: "StudentIntelligenceSnapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalSessions",
                table: "StudentIntelligenceSnapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AttendanceSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    DoctorId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    SubjectId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    TeachingAssistantId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    QrCodeContent = table.Column<string>(type: "text", nullable: false),
                    SessionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceSessions_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceSessions_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceSessions_TeachingAssistants_TeachingAssistantId",
                        column: x => x.TeachingAssistantId,
                        principalTable: "TeachingAssistants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentAttendances",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    AttendanceSessionId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    StudentId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    CheckInTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPresent = table.Column<bool>(type: "boolean", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAttendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentAttendances_AttendanceSessions_AttendanceSessionId",
                        column: x => x.AttendanceSessionId,
                        principalTable: "AttendanceSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentAttendances_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_DoctorId",
                table: "AttendanceSessions",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_SubjectId",
                table: "AttendanceSessions",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_TeachingAssistantId",
                table: "AttendanceSessions",
                column: "TeachingAssistantId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAttendances_AttendanceSessionId_StudentId",
                table: "StudentAttendances",
                columns: new[] { "AttendanceSessionId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentAttendances_StudentId",
                table: "StudentAttendances",
                column: "StudentId");
        }
    }
}
