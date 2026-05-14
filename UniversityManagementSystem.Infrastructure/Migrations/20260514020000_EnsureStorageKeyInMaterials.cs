using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using UniversityManagementSystem.Infrastructure.Data;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260514020000_EnsureStorageKeyInMaterials")]
    public class EnsureStorageKeyInMaterials : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: safe to run even if a previous attempt partially applied.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Materials""
                ADD COLUMN IF NOT EXISTS ""StorageKey"" text NOT NULL DEFAULT '';
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "Materials");
        }
    }
}
