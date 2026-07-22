using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.CandidateAccounts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "BirthDate",
                schema: "candidate_accounts",
                table: "CandidateAccounts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "candidate_accounts",
                table: "CandidateAccounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                schema: "candidate_accounts",
                table: "CandidateAccounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "candidate_accounts",
                table: "CandidateAccounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BirthDate",
                schema: "candidate_accounts",
                table: "CandidateAccounts");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "candidate_accounts",
                table: "CandidateAccounts");

            migrationBuilder.DropColumn(
                name: "Country",
                schema: "candidate_accounts",
                table: "CandidateAccounts");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "candidate_accounts",
                table: "CandidateAccounts");
        }
    }
}
