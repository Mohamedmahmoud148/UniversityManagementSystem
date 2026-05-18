using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationAndAcademicFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcademicYear_Colleges_CollegeId",
                table: "AcademicYear");

            migrationBuilder.DropForeignKey(
                name: "FK_AcademicYearDepartments_AcademicYear_AcademicYearId",
                table: "AcademicYearDepartments");

            migrationBuilder.DropForeignKey(
                name: "FK_Semester_AcademicYear_AcademicYearId",
                table: "Semester");

            migrationBuilder.DropForeignKey(
                name: "FK_SubjectOfferings_Semester_SemesterId",
                table: "SubjectOfferings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Semester",
                table: "Semester");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AcademicYear",
                table: "AcademicYear");

            migrationBuilder.RenameTable(
                name: "Semester",
                newName: "Semesters");

            migrationBuilder.RenameTable(
                name: "AcademicYear",
                newName: "AcademicYears");

            migrationBuilder.RenameIndex(
                name: "IX_Semester_Name_AcademicYearId",
                table: "Semesters",
                newName: "IX_Semesters_Name_AcademicYearId");

            migrationBuilder.RenameIndex(
                name: "IX_Semester_AcademicYearId",
                table: "Semesters",
                newName: "IX_Semesters_AcademicYearId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Semesters",
                table: "Semesters",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AcademicYears",
                table: "AcademicYears",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "AcademicPolicies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    DepartmentId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    DefaultMaxHours = table.Column<int>(type: "integer", nullable: false),
                    HonorMaxHours = table.Column<int>(type: "integer", nullable: false),
                    WarningMaxHours = table.Column<int>(type: "integer", nullable: false),
                    ProbationMaxHours = table.Column<int>(type: "integer", nullable: false),
                    WarningGpaThreshold = table.Column<double>(type: "double precision", nullable: false),
                    ProbationGpaThreshold = table.Column<double>(type: "double precision", nullable: false),
                    HonorGpaThreshold = table.Column<double>(type: "double precision", nullable: false),
                    GraduationMinGpa = table.Column<double>(type: "double precision", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcademicPolicies_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentAcademicStatuses",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    StudentId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    GPA = table.Column<double>(type: "double precision", nullable: false),
                    CGPA = table.Column<double>(type: "double precision", nullable: false),
                    LastSemesterGPA = table.Column<double>(type: "double precision", nullable: false),
                    LastCalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EarnedCreditHours = table.Column<int>(type: "integer", nullable: false),
                    RemainingCreditHours = table.Column<int>(type: "integer", nullable: false),
                    TotalRequiredHours = table.Column<int>(type: "integer", nullable: false),
                    Standing = table.Column<int>(type: "integer", nullable: false),
                    WarningCount = table.Column<int>(type: "integer", nullable: false),
                    CurrentLevel = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAcademicStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentAcademicStatuses_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubjectOfferingWaitlists",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    StudentId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    SubjectOfferingId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectOfferingWaitlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubjectOfferingWaitlists_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubjectOfferingWaitlists_SubjectOfferings_SubjectOfferingId",
                        column: x => x.SubjectOfferingId,
                        principalTable: "SubjectOfferings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubjectPrerequisites",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    PrerequisiteSubjectId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    MinimumGrade = table.Column<double>(type: "double precision", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectPrerequisites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubjectPrerequisites_Subjects_PrerequisiteSubjectId",
                        column: x => x.PrerequisiteSubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubjectPrerequisites_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicPolicies_DepartmentId",
                table: "AcademicPolicies",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAcademicStatuses_StudentId",
                table: "StudentAcademicStatuses",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubjectOfferingWaitlist_OfferingId",
                table: "SubjectOfferingWaitlists",
                column: "SubjectOfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectOfferingWaitlist_Student_Offering",
                table: "SubjectOfferingWaitlists",
                columns: new[] { "StudentId", "SubjectOfferingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubjectPrerequisites_PrerequisiteSubjectId",
                table: "SubjectPrerequisites",
                column: "PrerequisiteSubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectPrerequisites_Subject_Prereq",
                table: "SubjectPrerequisites",
                columns: new[] { "SubjectId", "PrerequisiteSubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubjectPrerequisites_SubjectId",
                table: "SubjectPrerequisites",
                column: "SubjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicYearDepartments_AcademicYears_AcademicYearId",
                table: "AcademicYearDepartments",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicYears_Colleges_CollegeId",
                table: "AcademicYears",
                column: "CollegeId",
                principalTable: "Colleges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Semesters_AcademicYears_AcademicYearId",
                table: "Semesters",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectOfferings_Semesters_SemesterId",
                table: "SubjectOfferings",
                column: "SemesterId",
                principalTable: "Semesters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcademicYearDepartments_AcademicYears_AcademicYearId",
                table: "AcademicYearDepartments");

            migrationBuilder.DropForeignKey(
                name: "FK_AcademicYears_Colleges_CollegeId",
                table: "AcademicYears");

            migrationBuilder.DropForeignKey(
                name: "FK_Semesters_AcademicYears_AcademicYearId",
                table: "Semesters");

            migrationBuilder.DropForeignKey(
                name: "FK_SubjectOfferings_Semesters_SemesterId",
                table: "SubjectOfferings");

            migrationBuilder.DropTable(
                name: "AcademicPolicies");

            migrationBuilder.DropTable(
                name: "StudentAcademicStatuses");

            migrationBuilder.DropTable(
                name: "SubjectOfferingWaitlists");

            migrationBuilder.DropTable(
                name: "SubjectPrerequisites");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Semesters",
                table: "Semesters");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AcademicYears",
                table: "AcademicYears");

            migrationBuilder.RenameTable(
                name: "Semesters",
                newName: "Semester");

            migrationBuilder.RenameTable(
                name: "AcademicYears",
                newName: "AcademicYear");

            migrationBuilder.RenameIndex(
                name: "IX_Semesters_Name_AcademicYearId",
                table: "Semester",
                newName: "IX_Semester_Name_AcademicYearId");

            migrationBuilder.RenameIndex(
                name: "IX_Semesters_AcademicYearId",
                table: "Semester",
                newName: "IX_Semester_AcademicYearId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Semester",
                table: "Semester",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AcademicYear",
                table: "AcademicYear",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicYear_Colleges_CollegeId",
                table: "AcademicYear",
                column: "CollegeId",
                principalTable: "Colleges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicYearDepartments_AcademicYear_AcademicYearId",
                table: "AcademicYearDepartments",
                column: "AcademicYearId",
                principalTable: "AcademicYear",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Semester_AcademicYear_AcademicYearId",
                table: "Semester",
                column: "AcademicYearId",
                principalTable: "AcademicYear",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectOfferings_Semester_SemesterId",
                table: "SubjectOfferings",
                column: "SemesterId",
                principalTable: "Semester",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
