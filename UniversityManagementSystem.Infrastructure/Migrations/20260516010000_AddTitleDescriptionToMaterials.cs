using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTitleDescriptionToMaterials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add Title and Description columns to Materials table.
            // These fields were added to the Material entity and DTO but a migration
            // was never created — causing 500 errors when EF tries to SELECT them.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Materials""
                ADD COLUMN IF NOT EXISTS ""Title"" text NOT NULL DEFAULT '';

                ALTER TABLE ""Materials""
                ADD COLUMN IF NOT EXISTS ""Description"" text NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Title",       table: "Materials");
            migrationBuilder.DropColumn(name: "Description", table: "Materials");
        }
    }
}
