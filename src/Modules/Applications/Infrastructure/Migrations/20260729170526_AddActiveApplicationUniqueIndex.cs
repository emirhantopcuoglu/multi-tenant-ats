using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Applications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveApplicationUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Applications_TenantId_JobId_CandidateId_Active",
                schema: "applications",
                table: "Applications",
                columns: new[] { "TenantId", "JobId", "CandidateId" },
                unique: true,
                filter: "\"Status\" = 'Active' AND NOT \"IsDeleted\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_TenantId_JobId_CandidateId_Active",
                schema: "applications",
                table: "Applications");
        }
    }
}
