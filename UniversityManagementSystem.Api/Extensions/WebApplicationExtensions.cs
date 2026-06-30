using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using UniversityManagementSystem.Api.Filters;
using UniversityManagementSystem.Api.Hubs;
using UniversityManagementSystem.Api.Middleware;
using UniversityManagementSystem.Infrastructure.Data;
// Explicit alias to resolve ambiguity between Microsoft.Extensions.Logging.ILogger and Serilog.ILogger
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace UniversityManagementSystem.Api.Extensions
{
    /// <summary>
    /// WebApplication extension methods for middleware and infrastructure configuration.
    /// Extracted from Program.cs for maintainability.
    /// </summary>
    public static class WebApplicationExtensions
    {
        // ── Middleware Pipeline ───────────────────────────────────────────────
        public static WebApplication ConfigureMiddlewares(this WebApplication app)
        {
            app.UseMiddleware<ExceptionMiddleware>();
            app.UseSerilogRequestLogging();
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseCors();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();
            app.MapControllers();
            app.MapHealthChecks("/health");
            return app;
        }

        // ── SignalR Hubs ──────────────────────────────────────────────────────
        public static WebApplication MapSignalRHubs(this WebApplication app)
        {
            app.MapHub<AuditHub>("/hubs/audit");
            app.MapHub<NotificationHub>("/hubs/notifications", options =>
            {
                options.Transports =
                    Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                    Microsoft.AspNetCore.Http.Connections.HttpTransportType.ServerSentEvents |
                    Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            });
            return app;
        }

        // ── Hangfire Dashboard ────────────────────────────────────────────────
        public static WebApplication MapHangfire(this WebApplication app)
        {
            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                Authorization = [new HangfireAuthorizationFilter()]
            });
            return app;
        }

        // ── Database Migration (safe, idempotent) ─────────────────────────────
        /// <summary>
        /// Applies EF Core migrations and idempotent SQL patches.
        ///
        /// ARCHITECTURE NOTE:
        /// The SQL patches here are a temporary stability measure until all hand-written
        /// migrations are regenerated with `dotnet ef migrations add`.
        /// Each patch uses IF EXISTS / IF NOT EXISTS guards to be safe on any DB state.
        ///
        /// MIGRATION DEBT TRACKER:
        /// - 20260619000000_ExpandAuditLog       → needs Designer.cs + Snapshot update
        /// - 20260619100000_AddLectureRecording   → needs Designer.cs + Snapshot update
        /// - 20260621011219_AddComplaintIntelligenceEnhancements → ComplaintClusters/ClusterReplies/
        ///   ClusterStatusHistories columns+tables patched here because the migration's AuditLogs
        ///   RenameColumn steps fail once PATCH 1 has already renamed those columns, aborting the
        ///   whole migration transaction (including the unrelated ComplaintCluster changes).
        /// See DATABASE_MIGRATION_RECOVERY_REPORT.md for remediation plan.
        /// </summary>
        public static async Task ApplyMigrationsAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

            try
            {
                // Step 1: Idempotent patches BEFORE Migrate() to prevent column-not-found errors
                await ApplyPreMigrationPatchesAsync(db, logger);

                // Step 2: EF Core migrations (handles EF-generated migrations)
                try
                {
                    await db.Database.MigrateAsync();
                    logger.LogInformation("Database migrations applied successfully.");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Migrate() failed — patches already applied. Continuing.");
                }

                // Step 3: Post-migration data patches
                await ApplyPostMigrationPatchesAsync(db, logger);

                // Step 4: Seed
                await DbInitializer.SeedAsync(scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Critical error during database initialization.");
            }
        }

        private static async Task ApplyPreMigrationPatchesAsync(
            AppDbContext db, ILogger logger)
        {
            // PATCH 1: AuditLog column rename (20260619000000_ExpandAuditLog)
            // Can be removed once migration has proper Designer.cs
            try
            {
                await db.Database.ExecuteSqlRawAsync(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='AuditLogs' AND column_name='ActionType') THEN
        ALTER TABLE ""AuditLogs"" RENAME COLUMN ""ActionType"" TO ""Action"";
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='AuditLogs' AND column_name='EntityName') THEN
        ALTER TABLE ""AuditLogs"" RENAME COLUMN ""EntityName"" TO ""Entity"";
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='AuditLogs' AND column_name='PerformedByUserId') THEN
        ALTER TABLE ""AuditLogs"" RENAME COLUMN ""PerformedByUserId"" TO ""UserId"";
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='AuditLogs' AND column_name='PerformedAt') THEN
        ALTER TABLE ""AuditLogs"" RENAME COLUMN ""PerformedAt"" TO ""Timestamp"";
    END IF;
    ALTER TABLE ""AuditLogs"" ADD COLUMN IF NOT EXISTS ""UserName""      text;
    ALTER TABLE ""AuditLogs"" ADD COLUMN IF NOT EXISTS ""Email""         text;
    ALTER TABLE ""AuditLogs"" ADD COLUMN IF NOT EXISTS ""Role""          text;
    ALTER TABLE ""AuditLogs"" ADD COLUMN IF NOT EXISTS ""Description""   text NOT NULL DEFAULT '';
    ALTER TABLE ""AuditLogs"" ADD COLUMN IF NOT EXISTS ""Severity""      integer NOT NULL DEFAULT 0;
    ALTER TABLE ""AuditLogs"" ADD COLUMN IF NOT EXISTS ""Status""        text NOT NULL DEFAULT 'Success';
    ALTER TABLE ""AuditLogs"" ADD COLUMN IF NOT EXISTS ""IpAddress""     text;
    ALTER TABLE ""AuditLogs"" ADD COLUMN IF NOT EXISTS ""UserAgent""     text;
    ALTER TABLE ""AuditLogs"" ADD COLUMN IF NOT EXISTS ""Browser""       text;
    ALTER TABLE ""AuditLogs"" ADD COLUMN IF NOT EXISTS ""Device""        text;
    ALTER TABLE ""AuditLogs"" ADD COLUMN IF NOT EXISTS ""CorrelationId"" text;
    ALTER TABLE ""AuditLogs"" ADD COLUMN IF NOT EXISTS ""RequestId""     text;
    ALTER TABLE ""AuditLogs"" ADD COLUMN IF NOT EXISTS ""DurationMs""    bigint;
    ALTER TABLE ""AuditLogs"" ADD COLUMN IF NOT EXISTS ""ChangedFields"" text;
    ALTER TABLE ""AuditLogs"" ADD COLUMN IF NOT EXISTS ""Metadata""      text;
    CREATE INDEX IF NOT EXISTS ""IX_AuditLogs_Timestamp"" ON ""AuditLogs""(""Timestamp"");
    CREATE INDEX IF NOT EXISTS ""IX_AuditLogs_Action""    ON ""AuditLogs""(""Action"");
    CREATE INDEX IF NOT EXISTS ""IX_AuditLogs_UserId""    ON ""AuditLogs""(""UserId"");
    CREATE INDEX IF NOT EXISTS ""IX_AuditLogs_Severity""  ON ""AuditLogs""(""Severity"");
END $$;");
                logger.LogInformation("AuditLog migration patch applied.");
            }
            catch (Exception ex) { logger.LogWarning(ex, "AuditLog patch (non-fatal)"); }

            // PATCH 2: LectureRecording tables (20260619100000_AddLectureRecording)
            // Can be removed once migration has proper Designer.cs
            try
            {
                await db.Database.ExecuteSqlRawAsync(@"
DO $$
BEGIN
    CREATE TABLE IF NOT EXISTS ""LectureRecordings"" (
        ""Id""               varchar(26) NOT NULL PRIMARY KEY,
        ""Code""             text NOT NULL DEFAULT '',
        ""StudentId""        varchar(26) NOT NULL,
        ""FileName""         text NOT NULL DEFAULT '',
        ""OriginalFileName"" text NOT NULL DEFAULT '',
        ""StoragePath""      text NOT NULL DEFAULT '',
        ""MimeType""         text NOT NULL DEFAULT '',
        ""FileSize""         bigint NOT NULL DEFAULT 0,
        ""DurationSeconds""  integer,
        ""Status""           varchar(20) NOT NULL DEFAULT 'Uploading',
        ""ErrorMessage""     text,
        ""TranscriptChars""  integer NOT NULL DEFAULT 0,
        ""ProcessedAt""      timestamp with time zone,
        ""CreatedAt""        timestamp with time zone NOT NULL DEFAULT NOW(),
        ""DeletedAt""        timestamp with time zone,
        CONSTRAINT ""FK_LectureRecordings_Students"" FOREIGN KEY (""StudentId"") REFERENCES ""Students""(""Id"") ON DELETE RESTRICT
    );
    ALTER TABLE ""LectureRecordings"" ADD COLUMN IF NOT EXISTS ""Code"" text NOT NULL DEFAULT '';
    CREATE INDEX IF NOT EXISTS ""IX_LectureRecordings_StudentId"" ON ""LectureRecordings""(""StudentId"");
    CREATE INDEX IF NOT EXISTS ""IX_LectureRecordings_Status""    ON ""LectureRecordings""(""Status"");

    CREATE TABLE IF NOT EXISTS ""LectureTranscripts"" (
        ""Id""          varchar(26) NOT NULL PRIMARY KEY, ""Code"" text NOT NULL DEFAULT '',
        ""RecordingId"" varchar(26) NOT NULL, ""ChunkIndex""  integer NOT NULL DEFAULT 0,
        ""Text""        text NOT NULL DEFAULT '', ""StartSecond"" integer, ""EndSecond"" integer,
        ""EmbeddingId"" text, ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
        ""DeletedAt""   timestamp with time zone,
        CONSTRAINT ""FK_LectureTranscripts_LectureRecordings"" FOREIGN KEY (""RecordingId"") REFERENCES ""LectureRecordings""(""Id"") ON DELETE CASCADE
    );
    ALTER TABLE ""LectureTranscripts"" ADD COLUMN IF NOT EXISTS ""Code"" text NOT NULL DEFAULT '';
    CREATE INDEX IF NOT EXISTS ""IX_LectureTranscripts_RecordingId"" ON ""LectureTranscripts""(""RecordingId"");

    CREATE TABLE IF NOT EXISTS ""LectureSummaries"" (
        ""Id""  varchar(26) NOT NULL PRIMARY KEY, ""Code"" text NOT NULL DEFAULT '',
        ""RecordingId"" varchar(26) NOT NULL, ""Summary"" text NOT NULL DEFAULT '',
        ""KeyConceptsJson"" text NOT NULL DEFAULT '[]', ""TimelineJson"" text NOT NULL DEFAULT '[]',
        ""SuggestedQuestionsJson"" text NOT NULL DEFAULT '[]',
        ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(), ""DeletedAt"" timestamp with time zone,
        CONSTRAINT ""FK_LectureSummaries_LectureRecordings"" FOREIGN KEY (""RecordingId"") REFERENCES ""LectureRecordings""(""Id"") ON DELETE CASCADE
    );
    ALTER TABLE ""LectureSummaries"" ADD COLUMN IF NOT EXISTS ""Code"" text NOT NULL DEFAULT '';
    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_LectureSummaries_RecordingId"" ON ""LectureSummaries""(""RecordingId"");

    CREATE TABLE IF NOT EXISTS ""LectureFlashcards"" (
        ""Id"" varchar(26) NOT NULL PRIMARY KEY, ""Code"" text NOT NULL DEFAULT '',
        ""RecordingId"" varchar(26) NOT NULL, ""Front"" text NOT NULL DEFAULT '',
        ""Back"" text NOT NULL DEFAULT '', ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
        ""DeletedAt"" timestamp with time zone,
        CONSTRAINT ""FK_LectureFlashcards_LectureRecordings"" FOREIGN KEY (""RecordingId"") REFERENCES ""LectureRecordings""(""Id"") ON DELETE CASCADE
    );
    ALTER TABLE ""LectureFlashcards"" ADD COLUMN IF NOT EXISTS ""Code"" text NOT NULL DEFAULT '';
    CREATE INDEX IF NOT EXISTS ""IX_LectureFlashcards_RecordingId"" ON ""LectureFlashcards""(""RecordingId"");

    CREATE TABLE IF NOT EXISTS ""LectureQuizzes"" (
        ""Id"" varchar(26) NOT NULL PRIMARY KEY, ""Code"" text NOT NULL DEFAULT '',
        ""RecordingId"" varchar(26) NOT NULL, ""Question"" text NOT NULL DEFAULT '',
        ""OptionA"" text NOT NULL DEFAULT '', ""OptionB"" text NOT NULL DEFAULT '',
        ""OptionC"" text NOT NULL DEFAULT '', ""OptionD"" text NOT NULL DEFAULT '',
        ""CorrectAnswer"" text NOT NULL DEFAULT '', ""Explanation"" text NOT NULL DEFAULT '',
        ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(), ""DeletedAt"" timestamp with time zone,
        CONSTRAINT ""FK_LectureQuizzes_LectureRecordings"" FOREIGN KEY (""RecordingId"") REFERENCES ""LectureRecordings""(""Id"") ON DELETE CASCADE
    );
    ALTER TABLE ""LectureQuizzes"" ADD COLUMN IF NOT EXISTS ""Code"" text NOT NULL DEFAULT '';
    CREATE INDEX IF NOT EXISTS ""IX_LectureQuizzes_RecordingId"" ON ""LectureQuizzes""(""RecordingId"");
END $$;");
                logger.LogInformation("LectureRecording migration patch applied.");
            }
            catch (Exception ex) { logger.LogWarning(ex, "LectureRecording patch (non-fatal)"); }

            // PATCH 3: ComplaintCluster enhancements (20260621011219_AddComplaintIntelligenceEnhancements)
            // Can be removed once migration has proper Designer.cs
            try
            {
                await db.Database.ExecuteSqlRawAsync(@"
DO $$
BEGIN
    ALTER TABLE ""ComplaintClusters"" ADD COLUMN IF NOT EXISTS ""AiRecommendations"" text;
    ALTER TABLE ""ComplaintClusters"" ADD COLUMN IF NOT EXISTS ""AverageSentiment""  double precision NOT NULL DEFAULT 0.0;
    ALTER TABLE ""ComplaintClusters"" ADD COLUMN IF NOT EXISTS ""CriticalCount""     integer NOT NULL DEFAULT 0;
    ALTER TABLE ""ComplaintClusters"" ADD COLUMN IF NOT EXISTS ""FirstComplaintAt""  timestamp with time zone NOT NULL DEFAULT NOW();
    ALTER TABLE ""ComplaintClusters"" ADD COLUMN IF NOT EXISTS ""ResolvedAt""        timestamp with time zone;
    ALTER TABLE ""ComplaintClusters"" ADD COLUMN IF NOT EXISTS ""Status""            character varying(50) NOT NULL DEFAULT 'Open';
    ALTER TABLE ""ComplaintClusters"" ADD COLUMN IF NOT EXISTS ""TrendDirection""    character varying(20) NOT NULL DEFAULT 'Stable';

    CREATE TABLE IF NOT EXISTS ""ClusterReplies"" (
        ""Id""                 varchar(26) NOT NULL PRIMARY KEY,
        ""ClusterId""          varchar(26) NOT NULL,
        ""RepliedByUserId""    varchar(26) NOT NULL,
        ""Message""            character varying(2000) NOT NULL DEFAULT '',
        ""AffectedStudents""   integer NOT NULL DEFAULT 0,
        ""NotificationsSent""  integer NOT NULL DEFAULT 0,
        ""Code""               text NOT NULL DEFAULT '',
        ""CreatedAt""          timestamp with time zone NOT NULL DEFAULT NOW(),
        ""DeletedAt""          timestamp with time zone,
        CONSTRAINT ""FK_ClusterReplies_ComplaintClusters"" FOREIGN KEY (""ClusterId"") REFERENCES ""ComplaintClusters""(""Id"") ON DELETE CASCADE
    );
    CREATE INDEX IF NOT EXISTS ""IX_ClusterReplies_ClusterId"" ON ""ClusterReplies""(""ClusterId"");

    CREATE TABLE IF NOT EXISTS ""ClusterStatusHistories"" (
        ""Id""               varchar(26) NOT NULL PRIMARY KEY,
        ""ClusterId""        varchar(26) NOT NULL,
        ""OldStatus""        character varying(50) NOT NULL DEFAULT '',
        ""NewStatus""        character varying(50) NOT NULL DEFAULT '',
        ""ChangedByUserId""  varchar(26) NOT NULL,
        ""Reason""           character varying(500),
        ""Code""             text NOT NULL DEFAULT '',
        ""CreatedAt""        timestamp with time zone NOT NULL DEFAULT NOW(),
        ""DeletedAt""        timestamp with time zone,
        CONSTRAINT ""FK_ClusterStatusHistories_ComplaintClusters"" FOREIGN KEY (""ClusterId"") REFERENCES ""ComplaintClusters""(""Id"") ON DELETE CASCADE
    );
    CREATE INDEX IF NOT EXISTS ""IX_ClusterStatusHistories_ClusterId"" ON ""ClusterStatusHistories""(""ClusterId"");
END $$;");
                logger.LogInformation("ComplaintCluster enhancements patch applied.");
            }
            catch (Exception ex) { logger.LogWarning(ex, "ComplaintCluster enhancements patch (non-fatal)"); }
        }

        private static async Task ApplyPostMigrationPatchesAsync(
            AppDbContext db, ILogger logger)
        {
            // Only data fixes here — no schema changes
            try
            {
                await db.Database.ExecuteSqlRawAsync(@"
                    ALTER TABLE ""UploadedFiles"" ADD COLUMN IF NOT EXISTS ""StorageKey"" text NOT NULL DEFAULT '';
                    ALTER TABLE ""UploadedFiles"" ADD COLUMN IF NOT EXISTS ""StoredFileName"" text NOT NULL DEFAULT '';");
            }
            catch (Exception ex) { logger.LogWarning(ex, "UploadedFiles patch (non-fatal)"); }
        }
    }
}
