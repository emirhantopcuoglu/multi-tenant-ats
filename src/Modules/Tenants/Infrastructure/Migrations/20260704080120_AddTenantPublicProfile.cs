using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Tenants.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPublicProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "tenants",
                table: "Tenants",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                schema: "tenants",
                table: "Tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                schema: "tenants",
                table: "Tenants",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                schema: "tenants",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Location",
                schema: "tenants",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Website",
                schema: "tenants",
                table: "Tenants");
        }
    }
}
