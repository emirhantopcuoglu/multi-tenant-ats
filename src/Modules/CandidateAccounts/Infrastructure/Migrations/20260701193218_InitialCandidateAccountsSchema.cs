using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.CandidateAccounts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCandidateAccountsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "candidate_accounts");

            migrationBuilder.CreateTable(
                name: "CandidateAccounts",
                schema: "candidate_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CvFileKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateAccounts_Email",
                schema: "candidate_accounts",
                table: "CandidateAccounts",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateAccounts",
                schema: "candidate_accounts");
        }
    }
}
