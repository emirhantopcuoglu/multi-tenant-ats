using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Jobs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJobTitleSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A trigram index serves the feed's lower("Title") LIKE '%term%' search. EF can't model an index on
            // an expression, so it is plain SQL and the snapshot doesn't change.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql("""CREATE INDEX "IX_Jobs_LowerTitle_Trgm" ON jobs."Jobs" USING gin (lower("Title") gin_trgm_ops);""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX jobs."IX_Jobs_LowerTitle_Trgm";""");
        }
    }
}
