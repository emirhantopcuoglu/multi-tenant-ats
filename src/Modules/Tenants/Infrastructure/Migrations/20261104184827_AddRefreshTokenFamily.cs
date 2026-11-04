using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Tenants.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenFamily : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FamilyId",
                schema: "tenants",
                table: "RefreshTokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Each existing token becomes its own family, as a fresh login would.
            migrationBuilder.Sql("UPDATE tenants.\"RefreshTokens\" SET \"FamilyId\" = \"Id\";");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "tenants",
                table: "RefreshTokens",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_FamilyId",
                schema: "tenants",
                table: "RefreshTokens",
                column: "FamilyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_FamilyId",
                schema: "tenants",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "FamilyId",
                schema: "tenants",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "tenants",
                table: "RefreshTokens");
        }
    }
}
