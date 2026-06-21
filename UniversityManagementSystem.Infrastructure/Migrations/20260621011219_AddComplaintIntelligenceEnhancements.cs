using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddComplaintIntelligenceEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PerformedByUserId",
                table: "AuditLogs",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "PerformedAt",
                table: "AuditLogs",
                newName: "Timestamp");

            migrationBuilder.RenameColumn(
                name: "EntityName",
                table: "AuditLogs",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "ActionType",
                table: "AuditLogs",
                newName: "Entity");

            migrationBuilder.AddColumn<string>(
                name: "AiRecommendations",
                table: "ComplaintClusters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageSentiment",
                table: "ComplaintClusters",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "CriticalCount",
                table: "ComplaintClusters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstComplaintAt",
                table: "ComplaintClusters",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "ComplaintClusters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ComplaintClusters",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Open");

            migrationBuilder.AddColumn<string>(
                name: "TrendDirection",
                table: "ComplaintClusters",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Stable");

            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Browser",
                table: "AuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangedFields",
                table: "AuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "AuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AuditLogs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Device",
                table: "AuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DurationMs",
                table: "AuditLogs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "AuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "AuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "AuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestId",
                table: "AuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "AuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Severity",
                table: "AuditLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "AuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "AuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClusterReplies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    ClusterId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    RepliedByUserId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AffectedStudents = table.Column<int>(type: "integer", nullable: false),
                    NotificationsSent = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClusterReplies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClusterReplies_ComplaintClusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "ComplaintClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClusterStatusHistories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    ClusterId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    OldStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NewStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChangedByUserId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClusterStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClusterStatusHistories_ComplaintClusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "ComplaintClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LectureRecordings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    StudentId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    StoragePath = table.Column<string>(type: "text", nullable: false),
                    MimeType = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    TranscriptChars = table.Column<int>(type: "integer", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureRecordings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LectureRecordings_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LectureFlashcards",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    RecordingId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Front = table.Column<string>(type: "text", nullable: false),
                    Back = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureFlashcards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LectureFlashcards_LectureRecordings_RecordingId",
                        column: x => x.RecordingId,
                        principalTable: "LectureRecordings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LectureQuizzes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    RecordingId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    OptionA = table.Column<string>(type: "text", nullable: false),
                    OptionB = table.Column<string>(type: "text", nullable: false),
                    OptionC = table.Column<string>(type: "text", nullable: false),
                    OptionD = table.Column<string>(type: "text", nullable: false),
                    CorrectAnswer = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureQuizzes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LectureQuizzes_LectureRecordings_RecordingId",
                        column: x => x.RecordingId,
                        principalTable: "LectureRecordings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LectureSummaries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    RecordingId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    KeyConceptsJson = table.Column<string>(type: "text", nullable: false),
                    TimelineJson = table.Column<string>(type: "text", nullable: false),
                    SuggestedQuestionsJson = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LectureSummaries_LectureRecordings_RecordingId",
                        column: x => x.RecordingId,
                        principalTable: "LectureRecordings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LectureTranscripts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    RecordingId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    StartSecond = table.Column<int>(type: "integer", nullable: true),
                    EndSecond = table.Column<int>(type: "integer", nullable: true),
                    EmbeddingId = table.Column<string>(type: "text", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureTranscripts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LectureTranscripts_LectureRecordings_RecordingId",
                        column: x => x.RecordingId,
                        principalTable: "LectureRecordings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Severity",
                table: "AuditLogs",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClusterReplies_ClusterId",
                table: "ClusterReplies",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_ClusterStatusHistories_ClusterId",
                table: "ClusterStatusHistories",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_LectureFlashcards_RecordingId",
                table: "LectureFlashcards",
                column: "RecordingId");

            migrationBuilder.CreateIndex(
                name: "IX_LectureQuizzes_RecordingId",
                table: "LectureQuizzes",
                column: "RecordingId");

            migrationBuilder.CreateIndex(
                name: "IX_LectureRecordings_Status",
                table: "LectureRecordings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LectureRecordings_StudentId",
                table: "LectureRecordings",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_LectureSummaries_RecordingId",
                table: "LectureSummaries",
                column: "RecordingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LectureTranscripts_RecordingId",
                table: "LectureTranscripts",
                column: "RecordingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClusterReplies");

            migrationBuilder.DropTable(
                name: "ClusterStatusHistories");

            migrationBuilder.DropTable(
                name: "LectureFlashcards");

            migrationBuilder.DropTable(
                name: "LectureQuizzes");

            migrationBuilder.DropTable(
                name: "LectureSummaries");

            migrationBuilder.DropTable(
                name: "LectureTranscripts");

            migrationBuilder.DropTable(
                name: "LectureRecordings");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Severity",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "AiRecommendations",
                table: "ComplaintClusters");

            migrationBuilder.DropColumn(
                name: "AverageSentiment",
                table: "ComplaintClusters");

            migrationBuilder.DropColumn(
                name: "CriticalCount",
                table: "ComplaintClusters");

            migrationBuilder.DropColumn(
                name: "FirstComplaintAt",
                table: "ComplaintClusters");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "ComplaintClusters");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ComplaintClusters");

            migrationBuilder.DropColumn(
                name: "TrendDirection",
                table: "ComplaintClusters");

            migrationBuilder.DropColumn(
                name: "Action",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Browser",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ChangedFields",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Device",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "DurationMs",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "AuditLogs");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "AuditLogs",
                newName: "PerformedByUserId");

            migrationBuilder.RenameColumn(
                name: "Timestamp",
                table: "AuditLogs",
                newName: "PerformedAt");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "AuditLogs",
                newName: "EntityName");

            migrationBuilder.RenameColumn(
                name: "Entity",
                table: "AuditLogs",
                newName: "ActionType");
        }
    }
}
