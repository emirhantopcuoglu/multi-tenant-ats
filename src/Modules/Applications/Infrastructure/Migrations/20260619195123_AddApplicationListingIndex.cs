using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Applications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationListingIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Applications_TenantId_AppliedAtUtc",
                schema: "applications",
                table: "Applications",
                columns: new[] { "TenantId", "AppliedAtUtc" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_TenantId_AppliedAtUtc",
                schema: "applications",
                table: "Applications");
        }
    }
}
