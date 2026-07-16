using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.CandidateAccounts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailChangeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailChangeRequests",
                schema: "candidate_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    NewEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailChangeRequests_CandidateAccounts_CandidateAccountId",
                        column: x => x.CandidateAccountId,
                        principalSchema: "candidate_accounts",
                        principalTable: "CandidateAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailChangeRequests_CandidateAccountId",
                schema: "candidate_accounts",
                table: "EmailChangeRequests",
                column: "CandidateAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailChangeRequests_TokenHash",
                schema: "candidate_accounts",
                table: "EmailChangeRequests",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailChangeRequests",
                schema: "candidate_accounts");
        }
    }
}
