using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResetData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Disable FK constraint enforcement so we can truncate in any order.
            migrationBuilder.Sql("SET session_replication_role = replica;");

            // Truncate all operational tables in one statement.
            // RESTART IDENTITY resets every associated sequence back to 1.
            // CASCADE drops any dependent data that might have been missed.
            // User-account tables (SystemUsers, Students, Doctors,
            // TeachingAssistants, Admins) are intentionally excluded so that
            // login credentials survive the reset.
            migrationBuilder.Sql(@"
                TRUNCATE TABLE
                    ""StudentExamVariants"",
                    ""ExamSubmissions"",
                    ""ExamQuestions"",
                    ""Exams"",
                    ""StudentAttendance"",
                    ""AttendanceSessions"",
                    ""StudentGrades"",
                    ""Enrollments"",
                    ""Materials"",
                    ""UploadedFiles"",
                    ""StudentFiles"",
                    ""EnrollmentUploads"",
                    ""ScheduleEntries"",
                    ""SubjectOfferings"",
                    ""SubjectDoctors"",
                    ""SubjectAssistants"",
                    ""Subjects"",
                    ""ChatMessages"",
                    ""Conversations"",
                    ""AiMemories"",
                    ""AiActionLogs"",
                    ""ComplaintAnalyses"",
                    ""ComplaintClusters"",
                    ""Complaints"",
                    ""AppNotifications"",
                    ""RefreshTokens"",
                    ""AuditLogs"",
                    ""RegulationSubjects"",
                    ""Regulations"",
                    ""AcademicYearDepartments"",
                    ""Semesters"",
                    ""AcademicYears"",
                    ""Groups"",
                    ""Batches"",
                    ""Departments"",
                    ""Colleges"",
                    ""Universities""
                RESTART IDENTITY CASCADE;
            ");

            // Re-enable FK constraint enforcement.
            migrationBuilder.Sql("SET session_replication_role = DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data truncation is irreversible — no rollback is possible.
        }
    }
}
