using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Tenants.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferredLanguage : Migration
    {
        // The company-side twin of AddCandidatePreferredLanguage; see that migration for why the
        // column is NOT NULL with a default instead of nullable plus a backfill.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                schema: "tenants",
                table: "AspNetUsers",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "en");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                schema: "tenants",
                table: "AspNetUsers");
        }
    }
}
