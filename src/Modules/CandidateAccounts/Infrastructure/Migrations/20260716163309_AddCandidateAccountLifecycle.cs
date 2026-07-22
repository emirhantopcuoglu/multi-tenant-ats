using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.CandidateAccounts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateAccountLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                schema: "candidate_accounts",
                table: "CandidateAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FrozenAtUtc",
                schema: "candidate_accounts",
                table: "CandidateAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "candidate_accounts",
                table: "CandidateAccounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                schema: "candidate_accounts",
                table: "CandidateAccounts");

            migrationBuilder.DropColumn(
                name: "FrozenAtUtc",
                schema: "candidate_accounts",
                table: "CandidateAccounts");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "candidate_accounts",
                table: "CandidateAccounts");
        }
    }
}
