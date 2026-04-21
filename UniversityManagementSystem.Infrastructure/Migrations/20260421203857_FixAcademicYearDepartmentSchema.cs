using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixAcademicYearDepartmentSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AcademicYear_Name",
                table: "AcademicYear");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "AcademicYearDepartments");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "AcademicYearDepartments");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_College_Name",
                table: "AcademicYear",
                columns: new[] { "CollegeId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AcademicYears_College_Name",
                table: "AcademicYear");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "AcademicYearDepartments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "AcademicYearDepartments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYear_Name",
                table: "AcademicYear",
                column: "Name",
                unique: true);
        }
    }
}
