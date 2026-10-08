using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.CandidateAccounts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidatePreferredLanguage : Migration
    {
        // Which language to write this candidate's emails in. NOT NULL with a default rather than
        // nullable-plus-a-backfill: Postgres fills existing rows from the default in the same
        // statement, so accounts that registered when the app only spoke English land on English —
        // which is what they were actually sent — with no second UPDATE to get wrong.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                schema: "candidate_accounts",
                table: "CandidateAccounts",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "en");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                schema: "candidate_accounts",
                table: "CandidateAccounts");
        }
    }
}
