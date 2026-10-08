using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.CandidateAccounts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateCvMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CvFileName",
                schema: "candidate_accounts",
                table: "CandidateAccounts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CvUploadedAtUtc",
                schema: "candidate_accounts",
                table: "CandidateAccounts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CvFileName",
                schema: "candidate_accounts",
                table: "CandidateAccounts");

            migrationBuilder.DropColumn(
                name: "CvUploadedAtUtc",
                schema: "candidate_accounts",
                table: "CandidateAccounts");
        }
    }
}
