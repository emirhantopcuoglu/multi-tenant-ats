using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Applications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationClosedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAtUtc",
                schema: "applications",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            // Closed before this column existed. The last change is never earlier than the real closing,
            // so their data is erased a little late, never early.
            migrationBuilder.Sql(
                "UPDATE applications.\"Applications\" SET \"ClosedAtUtc\" = COALESCE(\"ModifiedAtUtc\", \"AppliedAtUtc\") " +
                "WHERE \"Status\" <> 'Active';");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ClosedAtUtc",
                schema: "applications",
                table: "Applications",
                column: "ClosedAtUtc",
                filter: "\"ClosedAtUtc\" IS NOT NULL AND \"CvFileKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_ClosedAtUtc",
                schema: "applications",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ClosedAtUtc",
                schema: "applications",
                table: "Applications");
        }
    }
}
