using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateChatSystemMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsUserMessage",
                table: "ChatMessages",
                newName: "IsFallback");

            migrationBuilder.AddColumn<string>(
                name: "Sender",
                table: "ChatMessages",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Sender",
                table: "ChatMessages");

            migrationBuilder.RenameColumn(
                name: "IsFallback",
                table: "ChatMessages",
                newName: "IsUserMessage");
        }
    }
}
