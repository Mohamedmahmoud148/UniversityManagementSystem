using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLectureRecordingIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── LectureRecordings ─────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "LectureRecordings",
                columns: table => new
                {
                    Id               = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    StudentId        = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    FileName         = table.Column<string>(type: "text", nullable: false),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    StoragePath      = table.Column<string>(type: "text", nullable: false),
                    MimeType         = table.Column<string>(type: "text", nullable: false),
                    FileSize         = table.Column<long>(type: "bigint", nullable: false),
                    DurationSeconds  = table.Column<int>(type: "integer", nullable: true),
                    Status           = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Uploading"),
                    ErrorMessage     = table.Column<string>(type: "text", nullable: true),
                    TranscriptChars  = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ProcessedAt      = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt        = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt        = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureRecordings", x => x.Id);
                    table.ForeignKey("FK_LectureRecordings_Students_StudentId",
                        x => x.StudentId, "Students", "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex("IX_LectureRecordings_StudentId", "LectureRecordings", "StudentId");
            migrationBuilder.CreateIndex("IX_LectureRecordings_Status", "LectureRecordings", "Status");

            // ── LectureTranscripts ────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "LectureTranscripts",
                columns: table => new
                {
                    Id          = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    RecordingId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    ChunkIndex  = table.Column<int>(type: "integer", nullable: false),
                    Text        = table.Column<string>(type: "text", nullable: false),
                    StartSecond = table.Column<int>(type: "integer", nullable: true),
                    EndSecond   = table.Column<int>(type: "integer", nullable: true),
                    EmbeddingId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt   = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt   = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureTranscripts", x => x.Id);
                    table.ForeignKey("FK_LectureTranscripts_LectureRecordings_RecordingId",
                        x => x.RecordingId, "LectureRecordings", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_LectureTranscripts_RecordingId", "LectureTranscripts", "RecordingId");

            // ── LectureSummaries ──────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "LectureSummaries",
                columns: table => new
                {
                    Id                    = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    RecordingId           = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Summary               = table.Column<string>(type: "text", nullable: false),
                    KeyConceptsJson       = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    TimelineJson          = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    SuggestedQuestionsJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    CreatedAt             = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt             = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureSummaries", x => x.Id);
                    table.ForeignKey("FK_LectureSummaries_LectureRecordings_RecordingId",
                        x => x.RecordingId, "LectureRecordings", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_LectureSummaries_RecordingId", "LectureSummaries", "RecordingId", unique: true);

            // ── LectureFlashcards ─────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "LectureFlashcards",
                columns: table => new
                {
                    Id          = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    RecordingId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Front       = table.Column<string>(type: "text", nullable: false),
                    Back        = table.Column<string>(type: "text", nullable: false),
                    CreatedAt   = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt   = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureFlashcards", x => x.Id);
                    table.ForeignKey("FK_LectureFlashcards_LectureRecordings_RecordingId",
                        x => x.RecordingId, "LectureRecordings", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_LectureFlashcards_RecordingId", "LectureFlashcards", "RecordingId");

            // ── LectureQuizzes ────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "LectureQuizzes",
                columns: table => new
                {
                    Id            = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    RecordingId   = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Question      = table.Column<string>(type: "text", nullable: false),
                    OptionA       = table.Column<string>(type: "text", nullable: false),
                    OptionB       = table.Column<string>(type: "text", nullable: false),
                    OptionC       = table.Column<string>(type: "text", nullable: false),
                    OptionD       = table.Column<string>(type: "text", nullable: false),
                    CorrectAnswer = table.Column<string>(type: "text", nullable: false),
                    Explanation   = table.Column<string>(type: "text", nullable: false),
                    CreatedAt     = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt     = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureQuizzes", x => x.Id);
                    table.ForeignKey("FK_LectureQuizzes_LectureRecordings_RecordingId",
                        x => x.RecordingId, "LectureRecordings", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_LectureQuizzes_RecordingId", "LectureQuizzes", "RecordingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LectureQuizzes");
            migrationBuilder.DropTable(name: "LectureFlashcards");
            migrationBuilder.DropTable(name: "LectureSummaries");
            migrationBuilder.DropTable(name: "LectureTranscripts");
            migrationBuilder.DropTable(name: "LectureRecordings");
        }
    }
}
