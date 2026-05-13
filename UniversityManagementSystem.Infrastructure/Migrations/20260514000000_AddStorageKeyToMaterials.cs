using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageKeyToMaterials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add StorageKey column if it doesn't already exist.
            // The original AddStorageKeyColumn migration had this commented out
            // with "ALREADY EXISTS IN PROD" — which was incorrect for new deployments.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Materials""
                ADD COLUMN IF NOT EXISTS ""StorageKey"" text NOT NULL DEFAULT '';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "Materials");
        }
    }
}
