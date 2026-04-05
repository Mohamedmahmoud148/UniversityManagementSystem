using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApiRefactorFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Materials_UploadedFiles_FileId",
                table: "Materials");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentFiles_UploadedFiles_FileId",
                table: "StudentFiles");

            migrationBuilder.AlterColumn<string>(
                name: "FileId",
                table: "StudentFiles",
                type: "character varying(26)",
                maxLength: 26,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(26)",
                oldMaxLength: 26);

            migrationBuilder.AlterColumn<string>(
                name: "FileId",
                table: "Materials",
                type: "character varying(26)",
                maxLength: 26,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(26)",
                oldMaxLength: 26);

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
                name: "IX_Regulations_Code",
                table: "Regulations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exams_Code",
                table: "Exams",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_UploadedFiles_FileId",
                table: "Materials",
                column: "FileId",
                principalTable: "UploadedFiles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentFiles_UploadedFiles_FileId",
                table: "StudentFiles",
                column: "FileId",
                principalTable: "UploadedFiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Materials_UploadedFiles_FileId",
                table: "Materials");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentFiles_UploadedFiles_FileId",
                table: "StudentFiles");

            migrationBuilder.DropIndex(
                name: "IX_Universities_Code",
                table: "Universities");

            migrationBuilder.DropIndex(
                name: "IX_TeachingAssistants_Code",
                table: "TeachingAssistants");

            migrationBuilder.DropIndex(
                name: "IX_Regulations_Code",
                table: "Regulations");

            migrationBuilder.DropIndex(
                name: "IX_Exams_Code",
                table: "Exams");

            migrationBuilder.AlterColumn<string>(
                name: "FileId",
                table: "StudentFiles",
                type: "character varying(26)",
                maxLength: 26,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(26)",
                oldMaxLength: 26,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FileId",
                table: "Materials",
                type: "character varying(26)",
                maxLength: 26,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(26)",
                oldMaxLength: 26,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_UploadedFiles_FileId",
                table: "Materials",
                column: "FileId",
                principalTable: "UploadedFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentFiles_UploadedFiles_FileId",
                table: "StudentFiles",
                column: "FileId",
                principalTable: "UploadedFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
