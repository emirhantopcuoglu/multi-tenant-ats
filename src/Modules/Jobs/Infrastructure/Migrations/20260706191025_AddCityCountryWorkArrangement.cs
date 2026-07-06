using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Jobs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCityCountryWorkArrangement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Location",
                schema: "jobs",
                table: "Jobs",
                newName: "City");

            migrationBuilder.AddColumn<string>(
                name: "Country",
                schema: "jobs",
                table: "Jobs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkArrangement",
                schema: "jobs",
                table: "Jobs",
                type: "text",
                nullable: false,
                // Every existing job predates this concept; OnSite is the closest honest default for
                // a job posted before remote/hybrid work was tracked at all.
                defaultValue: "OnSite");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Country",
                schema: "jobs",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "WorkArrangement",
                schema: "jobs",
                table: "Jobs");

            migrationBuilder.RenameColumn(
                name: "City",
                schema: "jobs",
                table: "Jobs",
                newName: "Location");
        }
    }
}
