using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Interviews.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewReminderSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DayBeforeReminderDueAtUtc",
                schema: "interviews",
                table: "Interviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartingSoonReminderDueAtUtc",
                schema: "interviews",
                table: "Interviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_DayBeforeReminderDueAtUtc",
                schema: "interviews",
                table: "Interviews",
                column: "DayBeforeReminderDueAtUtc",
                filter: "\"DayBeforeReminderDueAtUtc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_StartingSoonReminderDueAtUtc",
                schema: "interviews",
                table: "Interviews",
                column: "StartingSoonReminderDueAtUtc",
                filter: "\"StartingSoonReminderDueAtUtc\" IS NOT NULL");

            // Backfill, so the feature covers the interviews already in everyone's calendar instead
            // of only those booked after this deploy. Without it the exact case the reminders exist
            // for — an interview scheduled weeks ahead — would be the one case that never gets one.
            //
            // The two offsets are written out rather than read from Interview's constants on
            // purpose: a migration is a record of what was applied at a point in time, and must keep
            // producing the same result after someone changes the lead time in code.
            //
            // Reminder points already behind us stay NULL, matching Interview.FutureOrNull: a
            // "tomorrow" nudge for an interview happening in an hour is worse than none.
            migrationBuilder.Sql("""
                UPDATE interviews."Interviews"
                SET "DayBeforeReminderDueAtUtc" =
                        CASE WHEN "ScheduledAtUtc" - INTERVAL '24 hours' > NOW()
                             THEN "ScheduledAtUtc" - INTERVAL '24 hours' END,
                    "StartingSoonReminderDueAtUtc" =
                        CASE WHEN "ScheduledAtUtc" - INTERVAL '10 minutes' > NOW()
                             THEN "ScheduledAtUtc" - INTERVAL '10 minutes' END
                WHERE "Status" = 'Scheduled'
                  AND NOT "IsDeleted"
                  AND "ScheduledAtUtc" > NOW();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Interviews_DayBeforeReminderDueAtUtc",
                schema: "interviews",
                table: "Interviews");

            migrationBuilder.DropIndex(
                name: "IX_Interviews_StartingSoonReminderDueAtUtc",
                schema: "interviews",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "DayBeforeReminderDueAtUtc",
                schema: "interviews",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "StartingSoonReminderDueAtUtc",
                schema: "interviews",
                table: "Interviews");
        }
    }
}
