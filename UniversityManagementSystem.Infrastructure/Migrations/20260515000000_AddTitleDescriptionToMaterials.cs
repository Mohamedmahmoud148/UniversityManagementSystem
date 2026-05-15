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
            migrationBuilder.Sql(@"
                ALTER TABLE ""Materials""
                ADD COLUMN IF NOT EXISTS ""Title"" TEXT NOT NULL DEFAULT '';

                ALTER TABLE ""Materials""
                ADD COLUMN IF NOT EXISTS ""Description"" TEXT NULL;

                -- Back-fill: use FileName as display title for existing rows
                UPDATE ""Materials"" SET ""Title"" = ""FileName"" WHERE ""Title"" = '';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Title", table: "Materials");
            migrationBuilder.DropColumn(name: "Description", table: "Materials");
        }
    }
}
