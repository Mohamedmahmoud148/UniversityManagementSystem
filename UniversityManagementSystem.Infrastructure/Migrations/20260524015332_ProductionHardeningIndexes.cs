using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProductionHardeningIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppNotifications_UserId",
                table: "AppNotifications");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentId",
                table: "Enrollments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppNotifications_UserId_CreatedAt",
                table: "AppNotifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppNotifications_UserId_IsRead",
                table: "AppNotifications",
                columns: new[] { "UserId", "IsRead" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Enrollments_StudentId",
                table: "Enrollments");

            migrationBuilder.DropIndex(
                name: "IX_AppNotifications_UserId_CreatedAt",
                table: "AppNotifications");

            migrationBuilder.DropIndex(
                name: "IX_AppNotifications_UserId_IsRead",
                table: "AppNotifications");

            migrationBuilder.CreateIndex(
                name: "IX_AppNotifications_UserId",
                table: "AppNotifications",
                column: "UserId");
        }
    }
}
