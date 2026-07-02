using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Ats.Modules.Applications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateAccountId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CandidateAccountId",
                schema: "applications",
                table: "Applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                schema: "applications",
                table: "Candidates",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('english', coalesce(\"FirstName\",'') || ' ' || coalesce(\"LastName\",'') || ' ' || coalesce(\"Email\",''))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_SearchVector",
                schema: "applications",
                table: "Candidates",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_CandidateAccountId",
                schema: "applications",
                table: "Applications",
                column: "CandidateAccountId",
                filter: "\"CandidateAccountId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Candidates_SearchVector",
                schema: "applications",
                table: "Candidates");

            migrationBuilder.DropIndex(
                name: "IX_Applications_CandidateAccountId",
                schema: "applications",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                schema: "applications",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "CandidateAccountId",
                schema: "applications",
                table: "Applications");
        }
    }
}
