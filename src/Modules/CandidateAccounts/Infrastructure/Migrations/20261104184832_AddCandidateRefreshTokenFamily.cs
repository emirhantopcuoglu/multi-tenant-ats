using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.CandidateAccounts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateRefreshTokenFamily : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FamilyId",
                schema: "candidate_accounts",
                table: "CandidateRefreshTokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Each existing token becomes its own family, as a fresh login would.
            migrationBuilder.Sql("UPDATE candidate_accounts.\"CandidateRefreshTokens\" SET \"FamilyId\" = \"Id\";");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "candidate_accounts",
                table: "CandidateRefreshTokens",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateRefreshTokens_FamilyId",
                schema: "candidate_accounts",
                table: "CandidateRefreshTokens",
                column: "FamilyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CandidateRefreshTokens_FamilyId",
                schema: "candidate_accounts",
                table: "CandidateRefreshTokens");

            migrationBuilder.DropColumn(
                name: "FamilyId",
                schema: "candidate_accounts",
                table: "CandidateRefreshTokens");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "candidate_accounts",
                table: "CandidateRefreshTokens");
        }
    }
}
