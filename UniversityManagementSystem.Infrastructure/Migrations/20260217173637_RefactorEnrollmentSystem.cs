using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorEnrollmentSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Enrollments_Students_StudentId",
                table: "Enrollments");

            migrationBuilder.DropForeignKey(
                name: "FK_Enrollments_SubjectOffering_SubjectOfferingId",
                table: "Enrollments");

            migrationBuilder.DropForeignKey(
                name: "FK_Enrollments_Subjects_SubjectId",
                table: "Enrollments");

            migrationBuilder.DropForeignKey(
                name: "FK_Exams_SubjectOffering_SubjectOfferingId",
                table: "Exams");

            migrationBuilder.DropForeignKey(
                name: "FK_Materials_SubjectOffering_SubjectOfferingId",
                table: "Materials");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentGrades_SubjectOffering_SubjectOfferingId",
                table: "StudentGrades");

            migrationBuilder.DropForeignKey(
                name: "FK_SubjectOffering_Batches_BatchId",
                table: "SubjectOffering");

            migrationBuilder.DropForeignKey(
                name: "FK_SubjectOffering_Departments_DepartmentId",
                table: "SubjectOffering");

            migrationBuilder.DropForeignKey(
                name: "FK_SubjectOffering_Doctors_DoctorId",
                table: "SubjectOffering");

            migrationBuilder.DropForeignKey(
                name: "FK_SubjectOffering_Groups_GroupId",
                table: "SubjectOffering");

            migrationBuilder.DropForeignKey(
                name: "FK_SubjectOffering_Semester_SemesterId",
                table: "SubjectOffering");

            migrationBuilder.DropForeignKey(
                name: "FK_SubjectOffering_Subjects_SubjectId",
                table: "SubjectOffering");

            migrationBuilder.DropForeignKey(
                name: "FK_UploadedFiles_SubjectOffering_SubjectOfferingId",
                table: "UploadedFiles");

            migrationBuilder.DropIndex(
                name: "IX_Enrollments_StudentId_SubjectId",
                table: "Enrollments");

            migrationBuilder.DropIndex(
                name: "IX_Enrollments_StudentId_SubjectOfferingId",
                table: "Enrollments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubjectOffering",
                table: "SubjectOffering");

            migrationBuilder.RenameTable(
                name: "SubjectOffering",
                newName: "SubjectOfferings");

            migrationBuilder.RenameIndex(
                name: "IX_SubjectOffering_SubjectId_SemesterId",
                table: "SubjectOfferings",
                newName: "IX_SubjectOfferings_SubjectId_SemesterId");

            migrationBuilder.RenameIndex(
                name: "IX_SubjectOffering_SemesterId",
                table: "SubjectOfferings",
                newName: "IX_SubjectOfferings_SemesterId");

            migrationBuilder.RenameIndex(
                name: "IX_SubjectOffering_GroupId",
                table: "SubjectOfferings",
                newName: "IX_SubjectOfferings_GroupId");

            migrationBuilder.RenameIndex(
                name: "IX_SubjectOffering_DoctorId",
                table: "SubjectOfferings",
                newName: "IX_SubjectOfferings_DoctorId");

            migrationBuilder.RenameIndex(
                name: "IX_SubjectOffering_DepartmentId",
                table: "SubjectOfferings",
                newName: "IX_SubjectOfferings_DepartmentId");

            migrationBuilder.RenameIndex(
                name: "IX_SubjectOffering_BatchId",
                table: "SubjectOfferings",
                newName: "IX_SubjectOfferings_BatchId");

            migrationBuilder.AlterColumn<int>(
                name: "SubjectOfferingId",
                table: "Enrollments",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SubjectId",
                table: "Enrollments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Enrollments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubjectOfferings",
                table: "SubjectOfferings",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentId_SubjectOfferingId",
                table: "Enrollments",
                columns: new[] { "StudentId", "SubjectOfferingId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Enrollments_Students_StudentId",
                table: "Enrollments",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Enrollments_SubjectOfferings_SubjectOfferingId",
                table: "Enrollments",
                column: "SubjectOfferingId",
                principalTable: "SubjectOfferings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Enrollments_Subjects_SubjectId",
                table: "Enrollments",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Exams_SubjectOfferings_SubjectOfferingId",
                table: "Exams",
                column: "SubjectOfferingId",
                principalTable: "SubjectOfferings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_SubjectOfferings_SubjectOfferingId",
                table: "Materials",
                column: "SubjectOfferingId",
                principalTable: "SubjectOfferings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentGrades_SubjectOfferings_SubjectOfferingId",
                table: "StudentGrades",
                column: "SubjectOfferingId",
                principalTable: "SubjectOfferings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectOfferings_Batches_BatchId",
                table: "SubjectOfferings",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectOfferings_Departments_DepartmentId",
                table: "SubjectOfferings",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectOfferings_Doctors_DoctorId",
                table: "SubjectOfferings",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectOfferings_Groups_GroupId",
                table: "SubjectOfferings",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectOfferings_Semester_SemesterId",
                table: "SubjectOfferings",
                column: "SemesterId",
                principalTable: "Semester",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectOfferings_Subjects_SubjectId",
                table: "SubjectOfferings",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UploadedFiles_SubjectOfferings_SubjectOfferingId",
                table: "UploadedFiles",
                column: "SubjectOfferingId",
                principalTable: "SubjectOfferings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Enrollments_Students_StudentId",
                table: "Enrollments");

            migrationBuilder.DropForeignKey(
                name: "FK_Enrollments_SubjectOfferings_SubjectOfferingId",
                table: "Enrollments");

            migrationBuilder.DropForeignKey(
                name: "FK_Enrollments_Subjects_SubjectId",
                table: "Enrollments");

            migrationBuilder.DropForeignKey(
                name: "FK_Exams_SubjectOfferings_SubjectOfferingId",
                table: "Exams");

            migrationBuilder.DropForeignKey(
                name: "FK_Materials_SubjectOfferings_SubjectOfferingId",
                table: "Materials");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentGrades_SubjectOfferings_SubjectOfferingId",
                table: "StudentGrades");

            migrationBuilder.DropForeignKey(
                name: "FK_SubjectOfferings_Batches_BatchId",
                table: "SubjectOfferings");

            migrationBuilder.DropForeignKey(
                name: "FK_SubjectOfferings_Departments_DepartmentId",
                table: "SubjectOfferings");

            migrationBuilder.DropForeignKey(
                name: "FK_SubjectOfferings_Doctors_DoctorId",
                table: "SubjectOfferings");

            migrationBuilder.DropForeignKey(
                name: "FK_SubjectOfferings_Groups_GroupId",
                table: "SubjectOfferings");

            migrationBuilder.DropForeignKey(
                name: "FK_SubjectOfferings_Semester_SemesterId",
                table: "SubjectOfferings");

            migrationBuilder.DropForeignKey(
                name: "FK_SubjectOfferings_Subjects_SubjectId",
                table: "SubjectOfferings");

            migrationBuilder.DropForeignKey(
                name: "FK_UploadedFiles_SubjectOfferings_SubjectOfferingId",
                table: "UploadedFiles");

            migrationBuilder.DropIndex(
                name: "IX_Enrollments_StudentId_SubjectOfferingId",
                table: "Enrollments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubjectOfferings",
                table: "SubjectOfferings");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Enrollments");

            migrationBuilder.RenameTable(
                name: "SubjectOfferings",
                newName: "SubjectOffering");

            migrationBuilder.RenameIndex(
                name: "IX_SubjectOfferings_SubjectId_SemesterId",
                table: "SubjectOffering",
                newName: "IX_SubjectOffering_SubjectId_SemesterId");

            migrationBuilder.RenameIndex(
                name: "IX_SubjectOfferings_SemesterId",
                table: "SubjectOffering",
                newName: "IX_SubjectOffering_SemesterId");

            migrationBuilder.RenameIndex(
                name: "IX_SubjectOfferings_GroupId",
                table: "SubjectOffering",
                newName: "IX_SubjectOffering_GroupId");

            migrationBuilder.RenameIndex(
                name: "IX_SubjectOfferings_DoctorId",
                table: "SubjectOffering",
                newName: "IX_SubjectOffering_DoctorId");

            migrationBuilder.RenameIndex(
                name: "IX_SubjectOfferings_DepartmentId",
                table: "SubjectOffering",
                newName: "IX_SubjectOffering_DepartmentId");

            migrationBuilder.RenameIndex(
                name: "IX_SubjectOfferings_BatchId",
                table: "SubjectOffering",
                newName: "IX_SubjectOffering_BatchId");

            migrationBuilder.AlterColumn<int>(
                name: "SubjectOfferingId",
                table: "Enrollments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "SubjectId",
                table: "Enrollments",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubjectOffering",
                table: "SubjectOffering",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentId_SubjectId",
                table: "Enrollments",
                columns: new[] { "StudentId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentId_SubjectOfferingId",
                table: "Enrollments",
                columns: new[] { "StudentId", "SubjectOfferingId" },
                unique: true,
                filter: "[SubjectOfferingId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Enrollments_Students_StudentId",
                table: "Enrollments",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Enrollments_SubjectOffering_SubjectOfferingId",
                table: "Enrollments",
                column: "SubjectOfferingId",
                principalTable: "SubjectOffering",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Enrollments_Subjects_SubjectId",
                table: "Enrollments",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Exams_SubjectOffering_SubjectOfferingId",
                table: "Exams",
                column: "SubjectOfferingId",
                principalTable: "SubjectOffering",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_SubjectOffering_SubjectOfferingId",
                table: "Materials",
                column: "SubjectOfferingId",
                principalTable: "SubjectOffering",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentGrades_SubjectOffering_SubjectOfferingId",
                table: "StudentGrades",
                column: "SubjectOfferingId",
                principalTable: "SubjectOffering",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectOffering_Batches_BatchId",
                table: "SubjectOffering",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectOffering_Departments_DepartmentId",
                table: "SubjectOffering",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectOffering_Doctors_DoctorId",
                table: "SubjectOffering",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectOffering_Groups_GroupId",
                table: "SubjectOffering",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectOffering_Semester_SemesterId",
                table: "SubjectOffering",
                column: "SemesterId",
                principalTable: "Semester",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectOffering_Subjects_SubjectId",
                table: "SubjectOffering",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UploadedFiles_SubjectOffering_SubjectOfferingId",
                table: "UploadedFiles",
                column: "SubjectOfferingId",
                principalTable: "SubjectOffering",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
