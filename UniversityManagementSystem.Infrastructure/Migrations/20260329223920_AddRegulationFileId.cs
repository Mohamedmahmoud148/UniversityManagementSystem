using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegulationFileId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "Regulations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "FileId",
                table: "Regulations",
                type: "character varying(26)",
                maxLength: 26,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Regulations_FileId",
                table: "Regulations",
                column: "FileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Regulations_UploadedFiles_FileId",
                table: "Regulations",
                column: "FileId",
                principalTable: "UploadedFiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Regulations_UploadedFiles_FileId",
                table: "Regulations");

            migrationBuilder.DropIndex(
                name: "IX_Regulations_FileId",
                table: "Regulations");

            migrationBuilder.DropColumn(
                name: "FileId",
                table: "Regulations");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "Regulations",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
