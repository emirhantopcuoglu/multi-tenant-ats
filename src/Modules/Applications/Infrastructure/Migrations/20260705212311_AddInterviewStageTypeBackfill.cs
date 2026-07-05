using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Applications.Infrastructure.Migrations
{
    /// <inheritdoc />
    // Data-only migration: PipelineStageType is stored as a plain string column
    // (HasConversion<string>), so adding the new Interview enum member needed no schema change.
    // This backfills stages created before that member existed, so pipelines built by earlier
    // versions of CreateDefault also gain the type the auto-advance-on-interview-scheduled
    // consumer depends on to find "the interview stage".
    public partial class AddInterviewStageTypeBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE applications."PipelineStages"
                SET "Type" = 'Interview'
                WHERE "Name" = 'Interview' AND "Type" = 'Active';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE applications."PipelineStages"
                SET "Type" = 'Active'
                WHERE "Name" = 'Interview' AND "Type" = 'Interview';
                """);
        }
    }
}
