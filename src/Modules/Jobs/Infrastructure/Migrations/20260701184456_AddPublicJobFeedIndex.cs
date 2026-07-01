using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Jobs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicJobFeedIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Status_PublishedAtUtc",
                schema: "jobs",
                table: "Jobs",
                columns: new[] { "Status", "PublishedAtUtc" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_Status_PublishedAtUtc",
                schema: "jobs",
                table: "Jobs");
        }
    }
}
