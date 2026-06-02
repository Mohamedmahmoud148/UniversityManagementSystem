using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiCompanionAndTeachingIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiCompanionProfiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    UserId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    WeakSubjects = table.Column<string>(type: "text", nullable: false),
                    StrongSubjects = table.Column<string>(type: "text", nullable: false),
                    LearningStyle = table.Column<string>(type: "text", nullable: false),
                    CurrentGoal = table.Column<string>(type: "text", nullable: false),
                    PreferredStudyTime = table.Column<string>(type: "text", nullable: false),
                    TotalSessions = table.Column<int>(type: "integer", nullable: false),
                    CurrentStreakDays = table.Column<int>(type: "integer", nullable: false),
                    LongestStreakDays = table.Column<int>(type: "integer", nullable: false),
                    LastInteractionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EngagementScore = table.Column<double>(type: "double precision", nullable: false),
                    LastRecommendations = table.Column<string>(type: "text", nullable: false),
                    GradeTrends = table.Column<string>(type: "text", nullable: false),
                    Milestones = table.Column<string>(type: "text", nullable: false),
                    ActiveGoals = table.Column<string>(type: "text", nullable: false),
                    TeachingStyle = table.Column<string>(type: "text", nullable: false),
                    ContentHistory = table.Column<string>(type: "text", nullable: false),
                    ProfileUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiCompanionProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiCompanionProfiles_SystemUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "SystemUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlashcardDecks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    UserId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    SubjectOfferingId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    TopicName = table.Column<string>(type: "text", nullable: false),
                    GeneratedBy = table.Column<string>(type: "text", nullable: false),
                    CardCount = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlashcardDecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlashcardDecks_SubjectOfferings_SubjectOfferingId",
                        column: x => x.SubjectOfferingId,
                        principalTable: "SubjectOfferings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FlashcardDecks_SystemUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "SystemUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentIntelligenceSnapshots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    StudentId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    SubjectOfferingId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    DoctorId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    StudentName = table.Column<string>(type: "text", nullable: false),
                    StudentUniversityId = table.Column<string>(type: "text", nullable: false),
                    BatchName = table.Column<string>(type: "text", nullable: false),
                    GroupName = table.Column<string>(type: "text", nullable: false),
                    DepartmentName = table.Column<string>(type: "text", nullable: false),
                    CollegeName = table.Column<string>(type: "text", nullable: false),
                    SubjectName = table.Column<string>(type: "text", nullable: false),
                    FinalScore = table.Column<double>(type: "double precision", nullable: true),
                    MidtermScore = table.Column<double>(type: "double precision", nullable: true),
                    CourseworkScore = table.Column<double>(type: "double precision", nullable: true),
                    FinalExamScore = table.Column<double>(type: "double precision", nullable: true),
                    GradeScore = table.Column<double>(type: "double precision", nullable: false),
                    TotalSessions = table.Column<int>(type: "integer", nullable: false),
                    AttendedSessions = table.Column<int>(type: "integer", nullable: false),
                    AttendancePercent = table.Column<double>(type: "double precision", nullable: false),
                    AttendanceScore = table.Column<double>(type: "double precision", nullable: false),
                    TotalAssignments = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAssignments = table.Column<int>(type: "integer", nullable: false),
                    LateSubmissions = table.Column<int>(type: "integer", nullable: false),
                    MissingAssignments = table.Column<int>(type: "integer", nullable: false),
                    AssignmentCompletionRate = table.Column<double>(type: "double precision", nullable: false),
                    AvgAssignmentGrade = table.Column<double>(type: "double precision", nullable: true),
                    TotalExams = table.Column<int>(type: "integer", nullable: false),
                    TakenExams = table.Column<int>(type: "integer", nullable: false),
                    AvgExamScore = table.Column<double>(type: "double precision", nullable: true),
                    AvgQuizScore = table.Column<double>(type: "double precision", nullable: true),
                    AiSessionCount = table.Column<int>(type: "integer", nullable: false),
                    AiStudyMinutes = table.Column<int>(type: "integer", nullable: false),
                    LearningStreakDays = table.Column<int>(type: "integer", nullable: false),
                    EngagementScore = table.Column<double>(type: "double precision", nullable: false),
                    RiskScore = table.Column<double>(type: "double precision", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    RiskFactors = table.Column<string>(type: "text", nullable: false),
                    RiskExplanation = table.Column<string>(type: "text", nullable: false),
                    RecommendedAction = table.Column<string>(type: "text", nullable: false),
                    AttendanceTrend = table.Column<double>(type: "double precision", nullable: false),
                    GradeTrend = table.Column<double>(type: "double precision", nullable: false),
                    OverallTrend = table.Column<string>(type: "text", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentIntelligenceSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentIntelligenceSnapshots_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentIntelligenceSnapshots_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentIntelligenceSnapshots_SubjectOfferings_SubjectOfferi~",
                        column: x => x.SubjectOfferingId,
                        principalTable: "SubjectOfferings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiInsights",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    UserId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    AiCompanionProfileId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    InsightType = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    DataPayload = table.Column<string>(type: "text", nullable: false),
                    ActionUrl = table.Column<string>(type: "text", nullable: true),
                    NotificationSent = table.Column<bool>(type: "boolean", nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeduplicationKey = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInsights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiInsights_AiCompanionProfiles_AiCompanionProfileId",
                        column: x => x.AiCompanionProfileId,
                        principalTable: "AiCompanionProfiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AiInsights_SystemUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "SystemUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    UserId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    SubjectOfferingId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    AiCompanionProfileId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    SessionType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TopicName = table.Column<string>(type: "text", nullable: false),
                    TotalQuestions = table.Column<int>(type: "integer", nullable: false),
                    CorrectAnswers = table.Column<int>(type: "integer", nullable: false),
                    AccuracyPercent = table.Column<double>(type: "double precision", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    AiFeedback = table.Column<string>(type: "text", nullable: false),
                    SessionData = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningSessions_AiCompanionProfiles_AiCompanionProfileId",
                        column: x => x.AiCompanionProfileId,
                        principalTable: "AiCompanionProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearningSessions_SubjectOfferings_SubjectOfferingId",
                        column: x => x.SubjectOfferingId,
                        principalTable: "SubjectOfferings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LearningSessions_SystemUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "SystemUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Flashcards",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    DeckId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Front = table.Column<string>(type: "text", nullable: false),
                    Back = table.Column<string>(type: "text", nullable: false),
                    Hint = table.Column<string>(type: "text", nullable: true),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    RepetitionCount = table.Column<int>(type: "integer", nullable: false),
                    EaseFactor = table.Column<double>(type: "double precision", nullable: false),
                    IntervalDays = table.Column<int>(type: "integer", nullable: false),
                    NextReviewAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flashcards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Flashcards_FlashcardDecks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "FlashcardDecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiCompanionProfiles_UserId",
                table: "AiCompanionProfiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiInsights_AiCompanionProfileId",
                table: "AiInsights",
                column: "AiCompanionProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AiInsights_UserId",
                table: "AiInsights",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardDecks_SubjectOfferingId",
                table: "FlashcardDecks",
                column: "SubjectOfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardDecks_UserId",
                table: "FlashcardDecks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Flashcards_DeckId",
                table: "Flashcards",
                column: "DeckId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_AiCompanionProfileId",
                table: "LearningSessions",
                column: "AiCompanionProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_SubjectOfferingId",
                table: "LearningSessions",
                column: "SubjectOfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_UserId",
                table: "LearningSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentIntelligenceSnapshots_DoctorId",
                table: "StudentIntelligenceSnapshots",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentIntelligenceSnapshots_StudentId",
                table: "StudentIntelligenceSnapshots",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentIntelligenceSnapshots_SubjectOfferingId",
                table: "StudentIntelligenceSnapshots",
                column: "SubjectOfferingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiInsights");

            migrationBuilder.DropTable(
                name: "Flashcards");

            migrationBuilder.DropTable(
                name: "LearningSessions");

            migrationBuilder.DropTable(
                name: "StudentIntelligenceSnapshots");

            migrationBuilder.DropTable(
                name: "FlashcardDecks");

            migrationBuilder.DropTable(
                name: "AiCompanionProfiles");
        }
    }
}
