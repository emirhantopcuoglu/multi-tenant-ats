using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Jobs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicJobListingIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_TenantId_Status",
                schema: "jobs",
                table: "Jobs");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_TenantId_Status_PublishedAtUtc",
                schema: "jobs",
                table: "Jobs",
                columns: new[] { "TenantId", "Status", "PublishedAtUtc" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_TenantId_Status_PublishedAtUtc",
                schema: "jobs",
                table: "Jobs");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_TenantId_Status",
                schema: "jobs",
                table: "Jobs",
                columns: new[] { "TenantId", "Status" });
        }
    }
}
