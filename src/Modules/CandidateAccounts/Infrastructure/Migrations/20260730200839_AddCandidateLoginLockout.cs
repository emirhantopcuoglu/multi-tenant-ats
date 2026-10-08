using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.CandidateAccounts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateLoginLockout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                schema: "candidate_accounts",
                table: "CandidateAccounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEndsAtUtc",
                schema: "candidate_accounts",
                table: "CandidateAccounts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                schema: "candidate_accounts",
                table: "CandidateAccounts");

            migrationBuilder.DropColumn(
                name: "LockoutEndsAtUtc",
                schema: "candidate_accounts",
                table: "CandidateAccounts");
        }
    }
}
