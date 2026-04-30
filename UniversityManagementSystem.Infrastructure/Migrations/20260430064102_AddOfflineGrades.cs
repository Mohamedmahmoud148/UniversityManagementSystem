using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOfflineGrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CourseworkMaxScore",
                table: "SubjectOfferings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "CourseworkWeight",
                table: "SubjectOfferings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "FinalExamMaxScore",
                table: "SubjectOfferings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "FinalExamWeight",
                table: "SubjectOfferings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "MidtermMaxScore",
                table: "SubjectOfferings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "MidtermWeight",
                table: "SubjectOfferings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PlatformMaxScore",
                table: "SubjectOfferings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PlatformWeight",
                table: "SubjectOfferings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "CourseworkScore",
                table: "StudentGrades",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FinalExamScore",
                table: "StudentGrades",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MidtermScore",
                table: "StudentGrades",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PlatformScore",
                table: "StudentGrades",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CourseworkMaxScore",
                table: "SubjectOfferings");

            migrationBuilder.DropColumn(
                name: "CourseworkWeight",
                table: "SubjectOfferings");

            migrationBuilder.DropColumn(
                name: "FinalExamMaxScore",
                table: "SubjectOfferings");

            migrationBuilder.DropColumn(
                name: "FinalExamWeight",
                table: "SubjectOfferings");

            migrationBuilder.DropColumn(
                name: "MidtermMaxScore",
                table: "SubjectOfferings");

            migrationBuilder.DropColumn(
                name: "MidtermWeight",
                table: "SubjectOfferings");

            migrationBuilder.DropColumn(
                name: "PlatformMaxScore",
                table: "SubjectOfferings");

            migrationBuilder.DropColumn(
                name: "PlatformWeight",
                table: "SubjectOfferings");

            migrationBuilder.DropColumn(
                name: "CourseworkScore",
                table: "StudentGrades");

            migrationBuilder.DropColumn(
                name: "FinalExamScore",
                table: "StudentGrades");

            migrationBuilder.DropColumn(
                name: "MidtermScore",
                table: "StudentGrades");

            migrationBuilder.DropColumn(
                name: "PlatformScore",
                table: "StudentGrades");
        }
    }
}
