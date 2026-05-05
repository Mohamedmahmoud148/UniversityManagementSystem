using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegulationDepartmentAndSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RegulationId",
                table: "Students",
                type: "character varying(26)",
                maxLength: 26,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepartmentId",
                table: "Regulations",
                type: "character varying(26)",
                maxLength: 26,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegulationId",
                table: "Batches",
                type: "character varying(26)",
                maxLength: 26,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RegulationSubjects",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    RegulationId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Semester = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulationSubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegulationSubjects_Regulations_RegulationId",
                        column: x => x.RegulationId,
                        principalTable: "Regulations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegulationSubjects_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Students_RegulationId",
                table: "Students",
                column: "RegulationId");

            migrationBuilder.CreateIndex(
                name: "IX_Regulations_DepartmentId",
                table: "Regulations",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_RegulationId",
                table: "Batches",
                column: "RegulationId");

            migrationBuilder.CreateIndex(
                name: "IX_RegulationSubjects_RegulationId_SubjectId",
                table: "RegulationSubjects",
                columns: new[] { "RegulationId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegulationSubjects_SubjectId",
                table: "RegulationSubjects",
                column: "SubjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Batches_Regulations_RegulationId",
                table: "Batches",
                column: "RegulationId",
                principalTable: "Regulations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Regulations_Departments_DepartmentId",
                table: "Regulations",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Regulations_RegulationId",
                table: "Students",
                column: "RegulationId",
                principalTable: "Regulations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Batches_Regulations_RegulationId",
                table: "Batches");

            migrationBuilder.DropForeignKey(
                name: "FK_Regulations_Departments_DepartmentId",
                table: "Regulations");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_Regulations_RegulationId",
                table: "Students");

            migrationBuilder.DropTable(
                name: "RegulationSubjects");

            migrationBuilder.DropIndex(
                name: "IX_Students_RegulationId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Regulations_DepartmentId",
                table: "Regulations");

            migrationBuilder.DropIndex(
                name: "IX_Batches_RegulationId",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "RegulationId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Regulations");

            migrationBuilder.DropColumn(
                name: "RegulationId",
                table: "Batches");
        }
    }
}
