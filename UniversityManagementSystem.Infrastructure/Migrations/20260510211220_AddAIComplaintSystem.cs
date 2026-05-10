using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAIComplaintSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ComplaintAnalyses_ComplaintClusters_ClusterId",
                table: "ComplaintAnalyses");

            migrationBuilder.DropForeignKey(
                name: "FK_Complaints_SubjectOfferings_SubjectOfferingId",
                table: "Complaints");

            migrationBuilder.DropForeignKey(
                name: "FK_Complaints_SystemUsers_UserId",
                table: "Complaints");

            migrationBuilder.DropIndex(
                name: "IX_Complaints_SubjectOfferingId",
                table: "Complaints");

            migrationBuilder.DropIndex(
                name: "IX_Complaints_Target_CreatedAt",
                table: "Complaints");

            migrationBuilder.DropIndex(
                name: "IX_Complaints_TargetDoctorId",
                table: "Complaints");

            migrationBuilder.DropIndex(
                name: "IX_ComplaintClusters_Target",
                table: "ComplaintClusters");

            migrationBuilder.DropIndex(
                name: "IX_ComplaintAnalyses_ClusterId",
                table: "ComplaintAnalyses");

            migrationBuilder.DropColumn(
                name: "IsAnonymous",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "SubjectOfferingId",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "TargetDoctorId",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "TargetName",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "ComplaintClusters");

            migrationBuilder.DropColumn(
                name: "IsEscalated",
                table: "ComplaintClusters");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "ComplaintClusters");

            migrationBuilder.DropColumn(
                name: "TargetName",
                table: "ComplaintClusters");

            migrationBuilder.DropColumn(
                name: "ClusterId",
                table: "ComplaintAnalyses");

            migrationBuilder.DropColumn(
                name: "IsProcessed",
                table: "ComplaintAnalyses");

            migrationBuilder.DropColumn(
                name: "RecommendedAction",
                table: "ComplaintAnalyses");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Complaints",
                newName: "StudentId");

            migrationBuilder.RenameIndex(
                name: "IX_Complaints_UserId",
                table: "Complaints",
                newName: "IX_Complaints_StudentId");

            migrationBuilder.RenameColumn(
                name: "Sentiment",
                table: "ComplaintAnalyses",
                newName: "SuggestedAction");

            migrationBuilder.RenameColumn(
                name: "RawAiResponse",
                table: "ComplaintAnalyses",
                newName: "DuplicateGroupId");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Complaints",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TargetId",
                table: "Complaints",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(26)",
                oldMaxLength: 26,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "Complaints",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Topic",
                table: "ComplaintClusters",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TargetType",
                table: "ComplaintClusters",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TargetId",
                table: "ComplaintClusters",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Severity",
                table: "ComplaintAnalyses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<double>(
                name: "SentimentScore",
                table: "ComplaintAnalyses",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(float),
                oldType: "real");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "ComplaintAnalyses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_TargetId",
                table: "Complaints",
                column: "TargetId");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_TargetType",
                table: "Complaints",
                column: "TargetType");

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintClusters_TargetId",
                table: "ComplaintClusters",
                column: "TargetId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintClusters_TargetType",
                table: "ComplaintClusters",
                column: "TargetType");

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintAnalyses_DuplicateGroupId",
                table: "ComplaintAnalyses",
                column: "DuplicateGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Complaints_SystemUsers_StudentId",
                table: "Complaints",
                column: "StudentId",
                principalTable: "SystemUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Complaints_SystemUsers_StudentId",
                table: "Complaints");

            migrationBuilder.DropIndex(
                name: "IX_Complaints_TargetId",
                table: "Complaints");

            migrationBuilder.DropIndex(
                name: "IX_Complaints_TargetType",
                table: "Complaints");

            migrationBuilder.DropIndex(
                name: "IX_ComplaintClusters_TargetId",
                table: "ComplaintClusters");

            migrationBuilder.DropIndex(
                name: "IX_ComplaintClusters_TargetType",
                table: "ComplaintClusters");

            migrationBuilder.DropIndex(
                name: "IX_ComplaintAnalyses_DuplicateGroupId",
                table: "ComplaintAnalyses");

            migrationBuilder.RenameColumn(
                name: "StudentId",
                table: "Complaints",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Complaints_StudentId",
                table: "Complaints",
                newName: "IX_Complaints_UserId");

            migrationBuilder.RenameColumn(
                name: "SuggestedAction",
                table: "ComplaintAnalyses",
                newName: "Sentiment");

            migrationBuilder.RenameColumn(
                name: "DuplicateGroupId",
                table: "ComplaintAnalyses",
                newName: "RawAiResponse");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Complaints",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "TargetId",
                table: "Complaints",
                type: "character varying(26)",
                maxLength: 26,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "Complaints",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<bool>(
                name: "IsAnonymous",
                table: "Complaints",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SubjectOfferingId",
                table: "Complaints",
                type: "character varying(26)",
                maxLength: 26,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetDoctorId",
                table: "Complaints",
                type: "character varying(26)",
                maxLength: 26,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetName",
                table: "Complaints",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Topic",
                table: "ComplaintClusters",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "TargetType",
                table: "ComplaintClusters",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "TargetId",
                table: "ComplaintClusters",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "ComplaintClusters",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsEscalated",
                table: "ComplaintClusters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "ComplaintClusters",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TargetName",
                table: "ComplaintClusters",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Severity",
                table: "ComplaintAnalyses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<float>(
                name: "SentimentScore",
                table: "ComplaintAnalyses",
                type: "real",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "ComplaintAnalyses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "ClusterId",
                table: "ComplaintAnalyses",
                type: "character varying(26)",
                maxLength: 26,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsProcessed",
                table: "ComplaintAnalyses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RecommendedAction",
                table: "ComplaintAnalyses",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_SubjectOfferingId",
                table: "Complaints",
                column: "SubjectOfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_Target_CreatedAt",
                table: "Complaints",
                columns: new[] { "TargetType", "TargetId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_TargetDoctorId",
                table: "Complaints",
                column: "TargetDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintClusters_Target",
                table: "ComplaintClusters",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintAnalyses_ClusterId",
                table: "ComplaintAnalyses",
                column: "ClusterId");

            migrationBuilder.AddForeignKey(
                name: "FK_ComplaintAnalyses_ComplaintClusters_ClusterId",
                table: "ComplaintAnalyses",
                column: "ClusterId",
                principalTable: "ComplaintClusters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Complaints_SubjectOfferings_SubjectOfferingId",
                table: "Complaints",
                column: "SubjectOfferingId",
                principalTable: "SubjectOfferings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Complaints_SystemUsers_UserId",
                table: "Complaints",
                column: "UserId",
                principalTable: "SystemUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
