using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fix_SoftDelete_FilteredUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Universities_Code",
                table: "Universities");

            migrationBuilder.DropIndex(
                name: "IX_TeachingAssistants_Code",
                table: "TeachingAssistants");

            migrationBuilder.DropIndex(
                name: "IX_TeachingAssistants_UniversityStaffId",
                table: "TeachingAssistants");

            migrationBuilder.DropIndex(
                name: "IX_SystemUsers_Code",
                table: "SystemUsers");

            migrationBuilder.DropIndex(
                name: "IX_SystemUsers_Email",
                table: "SystemUsers");

            migrationBuilder.DropIndex(
                name: "IX_SystemUsers_NationalId",
                table: "SystemUsers");

            migrationBuilder.DropIndex(
                name: "IX_SystemUsers_UniversityEmail",
                table: "SystemUsers");

            migrationBuilder.DropIndex(
                name: "IX_Subjects_Code",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_Students_Code",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_UniversityStudentId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Semesters_Name_AcademicYearId",
                table: "Semesters");

            migrationBuilder.DropIndex(
                name: "IX_Regulations_Code",
                table: "Regulations");

            migrationBuilder.DropIndex(
                name: "IX_Groups_Code",
                table: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_Exams_Code",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Doctors_Code",
                table: "Doctors");

            migrationBuilder.DropIndex(
                name: "IX_Doctors_UniversityStaffId",
                table: "Doctors");

            migrationBuilder.DropIndex(
                name: "IX_Departments_Code",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Colleges_Code",
                table: "Colleges");

            migrationBuilder.DropIndex(
                name: "IX_Batches_Code",
                table: "Batches");

            migrationBuilder.DropIndex(
                name: "IX_AcademicYears_College_Name",
                table: "AcademicYears");

            migrationBuilder.DropIndex(
                name: "IX_AcademicYears_College_Order",
                table: "AcademicYears");

            migrationBuilder.CreateIndex(
                name: "IX_Universities_Code",
                table: "Universities",
                column: "Code",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssistants_Code",
                table: "TeachingAssistants",
                column: "Code",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssistants_UniversityStaffId",
                table: "TeachingAssistants",
                column: "UniversityStaffId",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SystemUsers_Code",
                table: "SystemUsers",
                column: "Code",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SystemUsers_Email",
                table: "SystemUsers",
                column: "Email",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SystemUsers_NationalId",
                table: "SystemUsers",
                column: "NationalId",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SystemUsers_UniversityEmail",
                table: "SystemUsers",
                column: "UniversityEmail",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_Code",
                table: "Subjects",
                column: "Code",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Students_Code",
                table: "Students",
                column: "Code",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Students_UniversityStudentId",
                table: "Students",
                column: "UniversityStudentId",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Semesters_Name_AcademicYearId",
                table: "Semesters",
                columns: new[] { "Name", "AcademicYearId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Regulations_Code",
                table: "Regulations",
                column: "Code",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_Code",
                table: "Groups",
                column: "Code",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_Code",
                table: "Exams",
                column: "Code",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_Code",
                table: "Doctors",
                column: "Code",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_UniversityStaffId",
                table: "Doctors",
                column: "UniversityStaffId",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                table: "Departments",
                column: "Code",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Colleges_Code",
                table: "Colleges",
                column: "Code",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_Code",
                table: "Batches",
                column: "Code",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_College_Name",
                table: "AcademicYears",
                columns: new[] { "CollegeId", "Name" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_College_Order",
                table: "AcademicYears",
                columns: new[] { "CollegeId", "Order" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Universities_Code",
                table: "Universities");

            migrationBuilder.DropIndex(
                name: "IX_TeachingAssistants_Code",
                table: "TeachingAssistants");

            migrationBuilder.DropIndex(
                name: "IX_TeachingAssistants_UniversityStaffId",
                table: "TeachingAssistants");

            migrationBuilder.DropIndex(
                name: "IX_SystemUsers_Code",
                table: "SystemUsers");

            migrationBuilder.DropIndex(
                name: "IX_SystemUsers_Email",
                table: "SystemUsers");

            migrationBuilder.DropIndex(
                name: "IX_SystemUsers_NationalId",
                table: "SystemUsers");

            migrationBuilder.DropIndex(
                name: "IX_SystemUsers_UniversityEmail",
                table: "SystemUsers");

            migrationBuilder.DropIndex(
                name: "IX_Subjects_Code",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_Students_Code",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_UniversityStudentId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Semesters_Name_AcademicYearId",
                table: "Semesters");

            migrationBuilder.DropIndex(
                name: "IX_Regulations_Code",
                table: "Regulations");

            migrationBuilder.DropIndex(
                name: "IX_Groups_Code",
                table: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_Exams_Code",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Doctors_Code",
                table: "Doctors");

            migrationBuilder.DropIndex(
                name: "IX_Doctors_UniversityStaffId",
                table: "Doctors");

            migrationBuilder.DropIndex(
                name: "IX_Departments_Code",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Colleges_Code",
                table: "Colleges");

            migrationBuilder.DropIndex(
                name: "IX_Batches_Code",
                table: "Batches");

            migrationBuilder.DropIndex(
                name: "IX_AcademicYears_College_Name",
                table: "AcademicYears");

            migrationBuilder.DropIndex(
                name: "IX_AcademicYears_College_Order",
                table: "AcademicYears");

            migrationBuilder.CreateIndex(
                name: "IX_Universities_Code",
                table: "Universities",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssistants_Code",
                table: "TeachingAssistants",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssistants_UniversityStaffId",
                table: "TeachingAssistants",
                column: "UniversityStaffId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemUsers_Code",
                table: "SystemUsers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemUsers_Email",
                table: "SystemUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemUsers_NationalId",
                table: "SystemUsers",
                column: "NationalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemUsers_UniversityEmail",
                table: "SystemUsers",
                column: "UniversityEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_Code",
                table: "Subjects",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_Code",
                table: "Students",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_UniversityStudentId",
                table: "Students",
                column: "UniversityStudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Semesters_Name_AcademicYearId",
                table: "Semesters",
                columns: new[] { "Name", "AcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Regulations_Code",
                table: "Regulations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Groups_Code",
                table: "Groups",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exams_Code",
                table: "Exams",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_Code",
                table: "Doctors",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_UniversityStaffId",
                table: "Doctors",
                column: "UniversityStaffId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                table: "Departments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Colleges_Code",
                table: "Colleges",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Batches_Code",
                table: "Batches",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_College_Name",
                table: "AcademicYears",
                columns: new[] { "CollegeId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_College_Order",
                table: "AcademicYears",
                columns: new[] { "CollegeId", "Order" },
                unique: true);
        }
    }
}
