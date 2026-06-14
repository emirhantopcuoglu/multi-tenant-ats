using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Tenants.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditAndSoftDeleteToInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                schema: "tenants",
                table: "Invitations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "tenants",
                table: "Invitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                schema: "tenants",
                table: "Invitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                schema: "tenants",
                table: "Invitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "tenants",
                table: "Invitations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                schema: "tenants",
                table: "Invitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "tenants",
                table: "Invitations",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                schema: "tenants",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "tenants",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                schema: "tenants",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "tenants",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "tenants",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                schema: "tenants",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "tenants",
                table: "Invitations");
        }
    }
}
