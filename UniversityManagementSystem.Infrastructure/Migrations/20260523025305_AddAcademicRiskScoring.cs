using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicRiskScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcademicRiskScores",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    StudentId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    SubjectOfferingId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    AttendancePercent = table.Column<double>(type: "double precision", nullable: false),
                    AverageGrade = table.Column<double>(type: "double precision", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    AiRecommendation = table.Column<string>(type: "text", nullable: false),
                    AnalyzedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicRiskScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcademicRiskScores_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicRiskScores_SubjectOfferings_SubjectOfferingId",
                        column: x => x.SubjectOfferingId,
                        principalTable: "SubjectOfferings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Assignments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    SubjectOfferingId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    DoctorId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MaxGrade = table.Column<double>(type: "double precision", nullable: false),
                    AllowLateSubmission = table.Column<bool>(type: "boolean", nullable: false),
                    AiGradingEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    GradingRubric = table.Column<string>(type: "text", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assignments_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assignments_SubjectOfferings_SubjectOfferingId",
                        column: x => x.SubjectOfferingId,
                        principalTable: "SubjectOfferings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentSubmissions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    AssignmentId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    StudentId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    TextAnswer = table.Column<string>(type: "text", nullable: true),
                    FileUrl = table.Column<string>(type: "text", nullable: true),
                    StorageKey = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsLate = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Grade = table.Column<double>(type: "double precision", nullable: true),
                    Feedback = table.Column<string>(type: "text", nullable: true),
                    AiFeedback = table.Column<string>(type: "text", nullable: true),
                    Strengths = table.Column<string>(type: "text", nullable: true),
                    Weaknesses = table.Column<string>(type: "text", nullable: true),
                    IsAiGraded = table.Column<bool>(type: "boolean", nullable: false),
                    IsHumanReviewed = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewedByDoctorId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentSubmissions_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssignmentSubmissions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicRiskScores_RiskLevel",
                table: "AcademicRiskScores",
                column: "RiskLevel");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicRiskScores_Student_Offering",
                table: "AcademicRiskScores",
                columns: new[] { "StudentId", "SubjectOfferingId" });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicRiskScores_SubjectOfferingId",
                table: "AcademicRiskScores",
                column: "SubjectOfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_Deadline",
                table: "Assignments",
                column: "Deadline");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_DoctorId",
                table: "Assignments",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_SubjectOfferingId",
                table: "Assignments",
                column: "SubjectOfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentSubmissions_Assignment_Student",
                table: "AssignmentSubmissions",
                columns: new[] { "AssignmentId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentSubmissions_Status",
                table: "AssignmentSubmissions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentSubmissions_StudentId",
                table: "AssignmentSubmissions",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcademicRiskScores");

            migrationBuilder.DropTable(
                name: "AssignmentSubmissions");

            migrationBuilder.DropTable(
                name: "Assignments");
        }
    }
}
