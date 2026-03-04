using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IntroducePublicIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "Exams");

            migrationBuilder.CreateSequence<int>(
                name: "CollegeSequence");

            migrationBuilder.CreateSequence<int>(
                name: "DepartmentSequence");

            migrationBuilder.CreateSequence<int>(
                name: "DoctorSequence");

            migrationBuilder.CreateSequence<int>(
                name: "ExamSequence");

            migrationBuilder.CreateSequence<int>(
                name: "StudentSequence");

            migrationBuilder.CreateSequence<int>(
                name: "SubjectOfferingSequence");

            migrationBuilder.CreateSequence<int>(
                name: "SubjectSequence");

            migrationBuilder.AddColumn<string>(
                name: "PublicId",
                table: "Subjects",
                type: "text",
                nullable: false,
                defaultValueSql: "'SUB-' || cast(extract(year from current_date) as varchar) || '-' || nextval('\"SubjectSequence\"')");

            migrationBuilder.AddColumn<string>(
                name: "PublicId",
                table: "SubjectOfferings",
                type: "text",
                nullable: false,
                defaultValueSql: "'SO-' || cast(extract(year from current_date) as varchar) || '-' || nextval('\"SubjectOfferingSequence\"')");

            migrationBuilder.AddColumn<string>(
                name: "PublicId",
                table: "Students",
                type: "text",
                nullable: false,
                defaultValueSql: "'STU-' || cast(extract(year from current_date) as varchar) || '-' || nextval('\"StudentSequence\"')");

            migrationBuilder.AddColumn<int>(
                name: "CreatedByDoctorId",
                table: "Exams",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "Exams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "Exams",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PublicId",
                table: "Exams",
                type: "text",
                nullable: false,
                defaultValueSql: "'EX-' || cast(extract(year from current_date) as varchar) || '-' || nextval('\"ExamSequence\"')");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Exams",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PublicId",
                table: "Doctors",
                type: "text",
                nullable: false,
                defaultValueSql: "'DOC-' || cast(extract(year from current_date) as varchar) || '-' || nextval('\"DoctorSequence\"')");

            migrationBuilder.AddColumn<string>(
                name: "PublicId",
                table: "Departments",
                type: "text",
                nullable: false,
                defaultValueSql: "'DEP-' || cast(extract(year from current_date) as varchar) || '-' || nextval('\"DepartmentSequence\"')");

            migrationBuilder.AddColumn<string>(
                name: "PublicId",
                table: "Colleges",
                type: "text",
                nullable: false,
                defaultValueSql: "'COL-' || cast(extract(year from current_date) as varchar) || '-' || nextval('\"CollegeSequence\"')");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_PublicId",
                table: "Subjects",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubjectOfferings_PublicId",
                table: "SubjectOfferings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_PublicId",
                table: "Students",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exams_CreatedByDoctorId",
                table: "Exams",
                column: "CreatedByDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_PublicId",
                table: "Exams",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_PublicId",
                table: "Doctors",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_PublicId",
                table: "Departments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Colleges_PublicId",
                table: "Colleges",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Exams_Doctors_CreatedByDoctorId",
                table: "Exams",
                column: "CreatedByDoctorId",
                principalTable: "Doctors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Exams_Doctors_CreatedByDoctorId",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Subjects_PublicId",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_SubjectOfferings_PublicId",
                table: "SubjectOfferings");

            migrationBuilder.DropIndex(
                name: "IX_Students_PublicId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Exams_CreatedByDoctorId",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Exams_PublicId",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Doctors_PublicId",
                table: "Doctors");

            migrationBuilder.DropIndex(
                name: "IX_Departments_PublicId",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Colleges_PublicId",
                table: "Colleges");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "SubjectOfferings");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "CreatedByDoctorId",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Colleges");

            migrationBuilder.DropSequence(
                name: "CollegeSequence");

            migrationBuilder.DropSequence(
                name: "DepartmentSequence");

            migrationBuilder.DropSequence(
                name: "DoctorSequence");

            migrationBuilder.DropSequence(
                name: "ExamSequence");

            migrationBuilder.DropSequence(
                name: "StudentSequence");

            migrationBuilder.DropSequence(
                name: "SubjectOfferingSequence");

            migrationBuilder.DropSequence(
                name: "SubjectSequence");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Exams",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
