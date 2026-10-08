using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.CandidateAccounts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateEmailVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerifiedAtUtc",
                schema: "candidate_accounts",
                table: "CandidateAccounts",
                type: "timestamp with time zone",
                nullable: true);

            // Grandfather every account that already exists. They registered when no verification was
            // asked of them, so leaving them NULL would retroactively bar them from applying for a rule
            // they were never given the chance to satisfy — and their only route back would be a
            // "resend" to an address we would then be treating as unproven anyway.
            //
            // now() rather than CreatedAtUtc on purpose: the column then says what actually happened
            // — these accounts were granted verified status at this migration — instead of inventing a
            // confirmation click that never occurred. Only new registrations face the real rule.
            migrationBuilder.Sql("""
                UPDATE candidate_accounts."CandidateAccounts"
                SET "EmailVerifiedAtUtc" = now()
                WHERE "EmailVerifiedAtUtc" IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "EmailVerificationRequests",
                schema: "candidate_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailVerificationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailVerificationRequests_CandidateAccounts_CandidateAccoun~",
                        column: x => x.CandidateAccountId,
                        principalSchema: "candidate_accounts",
                        principalTable: "CandidateAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationRequests_CandidateAccountId",
                schema: "candidate_accounts",
                table: "EmailVerificationRequests",
                column: "CandidateAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationRequests_TokenHash",
                schema: "candidate_accounts",
                table: "EmailVerificationRequests",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailVerificationRequests",
                schema: "candidate_accounts");

            migrationBuilder.DropColumn(
                name: "EmailVerifiedAtUtc",
                schema: "candidate_accounts",
                table: "CandidateAccounts");
        }
    }
}
