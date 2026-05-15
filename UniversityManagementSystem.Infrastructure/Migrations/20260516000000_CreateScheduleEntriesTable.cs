using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateScheduleEntriesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create ScheduleEntries table if it doesn't already exist.
            // The table was in the EF model snapshot but never created in production
            // because the original migration was either commented out or missing.
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ScheduleEntries"" (
                    ""Id""                  character varying(26)   NOT NULL,
                    ""Code""                text                    NOT NULL DEFAULT '',
                    ""SubjectOfferingId""   character varying(26)   NOT NULL,
                    ""BatchId""             character varying(26)   NOT NULL,
                    ""GroupId""             character varying(26)   NULL,
                    ""DayOfWeek""           integer                 NOT NULL,
                    ""StartTime""           interval                NOT NULL,
                    ""EndTime""             interval                NOT NULL,
                    ""Type""                character varying(20)   NOT NULL DEFAULT 'Lecture',
                    ""Location""            text                    NOT NULL DEFAULT '',
                    ""WeekType""            character varying(10)   NOT NULL DEFAULT 'All',
                    ""IsActive""            boolean                 NOT NULL DEFAULT true,
                    ""CreatedAt""           timestamp with time zone NOT NULL DEFAULT now(),
                    ""DeletedAt""           timestamp with time zone NULL,
                    CONSTRAINT ""PK_ScheduleEntries"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_ScheduleEntries_SubjectOfferings_SubjectOfferingId""
                        FOREIGN KEY (""SubjectOfferingId"") REFERENCES ""SubjectOfferings"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_ScheduleEntries_Batches_BatchId""
                        FOREIGN KEY (""BatchId"") REFERENCES ""Batches"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_ScheduleEntries_Groups_GroupId""
                        FOREIGN KEY (""GroupId"") REFERENCES ""Groups"" (""Id"") ON DELETE SET NULL
                );
            ");

            // Indexes for common query patterns
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ScheduleEntries_OfferingId""
                    ON ""ScheduleEntries"" (""SubjectOfferingId"");

                CREATE INDEX IF NOT EXISTS ""IX_ScheduleEntries_Batch_Day""
                    ON ""ScheduleEntries"" (""BatchId"", ""DayOfWeek"");

                CREATE INDEX IF NOT EXISTS ""IX_ScheduleEntries_GroupId""
                    ON ""ScheduleEntries"" (""GroupId"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ScheduleEntries");
        }
    }
}
