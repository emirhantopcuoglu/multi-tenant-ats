using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Ats.Modules.Applications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateSearchVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Candidates_SearchVector",
                schema: "applications",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                schema: "applications",
                table: "Candidates");
        }
    }
}
