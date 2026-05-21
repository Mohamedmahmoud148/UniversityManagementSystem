using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamSessionAndAnalyticsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DraftAnswersJson",
                table: "ExamSubmissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                table: "ExamSubmissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSavedAt",
                table: "ExamSubmissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowLateSubmission",
                table: "Exams",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "Exams",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "Exams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LateSubmissionWindowMinutes",
                table: "Exams",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DraftAnswersJson",
                table: "ExamSubmissions");

            migrationBuilder.DropColumn(
                name: "IsCompleted",
                table: "ExamSubmissions");

            migrationBuilder.DropColumn(
                name: "LastSavedAt",
                table: "ExamSubmissions");

            migrationBuilder.DropColumn(
                name: "AllowLateSubmission",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "LateSubmissionWindowMinutes",
                table: "Exams");
        }
    }
}
