using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicYearCollegeAndDepartmentMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CollegeId",
                table: "AcademicYear",
                type: "character varying(26)",
                maxLength: 26,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "AcademicYear",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AcademicYearDepartments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    AcademicYearId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    DepartmentId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicYearDepartments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcademicYearDepartments_AcademicYear_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AcademicYearDepartments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_College_Order",
                table: "AcademicYear",
                columns: new[] { "CollegeId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYearDepartments_DepartmentId",
                table: "AcademicYearDepartments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYearDepartments_Year_Dept",
                table: "AcademicYearDepartments",
                columns: new[] { "AcademicYearId", "DepartmentId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicYear_Colleges_CollegeId",
                table: "AcademicYear",
                column: "CollegeId",
                principalTable: "Colleges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcademicYear_Colleges_CollegeId",
                table: "AcademicYear");

            migrationBuilder.DropTable(
                name: "AcademicYearDepartments");

            migrationBuilder.DropIndex(
                name: "IX_AcademicYears_College_Order",
                table: "AcademicYear");

            migrationBuilder.DropColumn(
                name: "CollegeId",
                table: "AcademicYear");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "AcademicYear");
        }
    }
}
