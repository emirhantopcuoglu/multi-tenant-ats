using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.CandidateAccounts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CandidateRefreshTokens",
                schema: "candidate_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: false),
                    SecurityStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateRefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateRefreshTokens_CandidateAccounts_CandidateAccountId",
                        column: x => x.CandidateAccountId,
                        principalSchema: "candidate_accounts",
                        principalTable: "CandidateAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateRefreshTokens_CandidateAccountId",
                schema: "candidate_accounts",
                table: "CandidateRefreshTokens",
                column: "CandidateAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateRefreshTokens_TokenHash",
                schema: "candidate_accounts",
                table: "CandidateRefreshTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateRefreshTokens",
                schema: "candidate_accounts");
        }
    }
}
