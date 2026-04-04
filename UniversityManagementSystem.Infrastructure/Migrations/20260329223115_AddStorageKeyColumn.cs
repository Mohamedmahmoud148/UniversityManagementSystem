using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageKeyColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ALREADY EXISTS IN PROD
            // migrationBuilder.AddColumn<string>(
            //     name: "StorageKey",
            //     table: "UploadedFiles",
            //     type: "text",
            //     nullable: false,
            //     defaultValue: "");

            // ALREADY EXISTS IN PROD
            // migrationBuilder.AddColumn<string>(
            //     name: "StorageKey",
            //     table: "Materials",
            //     type: "text",
            //     nullable: false,
            //     defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_SystemUsers_Code",
                table: "SystemUsers",
                column: "Code",
                unique: false);

            migrationBuilder.CreateIndex(
                name: "IX_Students_Code",
                table: "Students",
                column: "Code",
                unique: false);

            migrationBuilder.CreateIndex(
                name: "IX_Groups_Code",
                table: "Groups",
                column: "Code",
                unique: false);

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_Code",
                table: "Doctors",
                column: "Code",
                unique: false);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                table: "Departments",
                column: "Code",
                unique: false);

            migrationBuilder.CreateIndex(
                name: "IX_Colleges_Code",
                table: "Colleges",
                column: "Code",
                unique: false);

            migrationBuilder.CreateIndex(
                name: "IX_Batches_Code",
                table: "Batches",
                column: "Code",
                unique: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SystemUsers_Code",
                table: "SystemUsers");

            migrationBuilder.DropIndex(
                name: "IX_Students_Code",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Groups_Code",
                table: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_Doctors_Code",
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

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "UploadedFiles");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "Materials");
        }
    }
}
