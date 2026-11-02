using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Applications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "applications",
                table: "Applications",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "applications",
                table: "Applications");
        }
    }
}
