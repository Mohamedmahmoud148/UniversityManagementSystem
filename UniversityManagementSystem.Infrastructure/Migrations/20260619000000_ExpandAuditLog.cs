using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Rename legacy columns ─────────────────────────────────────────
            migrationBuilder.RenameColumn(name: "ActionType",        table: "AuditLogs", newName: "Action");
            migrationBuilder.RenameColumn(name: "EntityName",        table: "AuditLogs", newName: "Entity");
            migrationBuilder.RenameColumn(name: "PerformedByUserId", table: "AuditLogs", newName: "UserId");
            migrationBuilder.RenameColumn(name: "PerformedAt",       table: "AuditLogs", newName: "Timestamp");

            // ── New columns ───────────────────────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "UserName", table: "AuditLogs", type: "text", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email", table: "AuditLogs", type: "text", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role", table: "AuditLogs", type: "text", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description", table: "AuditLogs", type: "text", nullable: false, defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Severity", table: "AuditLogs", type: "integer", nullable: false, defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Status", table: "AuditLogs", type: "text", nullable: false, defaultValue: "Success");

            migrationBuilder.AddColumn<string>(
                name: "IpAddress", table: "AuditLogs", type: "text", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent", table: "AuditLogs", type: "text", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Browser", table: "AuditLogs", type: "text", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Device", table: "AuditLogs", type: "text", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId", table: "AuditLogs", type: "text", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestId", table: "AuditLogs", type: "text", nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DurationMs", table: "AuditLogs", type: "bigint", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangedFields", table: "AuditLogs", type: "text", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metadata", table: "AuditLogs", type: "text", nullable: true);

            // ── Performance indexes ───────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Severity",
                table: "AuditLogs",
                column: "Severity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_AuditLogs_Timestamp", table: "AuditLogs");
            migrationBuilder.DropIndex(name: "IX_AuditLogs_Action",    table: "AuditLogs");
            migrationBuilder.DropIndex(name: "IX_AuditLogs_UserId",    table: "AuditLogs");
            migrationBuilder.DropIndex(name: "IX_AuditLogs_Severity",  table: "AuditLogs");

            migrationBuilder.DropColumn(name: "UserName",      table: "AuditLogs");
            migrationBuilder.DropColumn(name: "Email",         table: "AuditLogs");
            migrationBuilder.DropColumn(name: "Role",          table: "AuditLogs");
            migrationBuilder.DropColumn(name: "Description",   table: "AuditLogs");
            migrationBuilder.DropColumn(name: "Severity",      table: "AuditLogs");
            migrationBuilder.DropColumn(name: "Status",        table: "AuditLogs");
            migrationBuilder.DropColumn(name: "IpAddress",     table: "AuditLogs");
            migrationBuilder.DropColumn(name: "UserAgent",     table: "AuditLogs");
            migrationBuilder.DropColumn(name: "Browser",       table: "AuditLogs");
            migrationBuilder.DropColumn(name: "Device",        table: "AuditLogs");
            migrationBuilder.DropColumn(name: "CorrelationId", table: "AuditLogs");
            migrationBuilder.DropColumn(name: "RequestId",     table: "AuditLogs");
            migrationBuilder.DropColumn(name: "DurationMs",    table: "AuditLogs");
            migrationBuilder.DropColumn(name: "ChangedFields", table: "AuditLogs");
            migrationBuilder.DropColumn(name: "Metadata",      table: "AuditLogs");

            migrationBuilder.RenameColumn(name: "Action",    table: "AuditLogs", newName: "ActionType");
            migrationBuilder.RenameColumn(name: "Entity",    table: "AuditLogs", newName: "EntityName");
            migrationBuilder.RenameColumn(name: "UserId",    table: "AuditLogs", newName: "PerformedByUserId");
            migrationBuilder.RenameColumn(name: "Timestamp", table: "AuditLogs", newName: "PerformedAt");
        }
    }
}
