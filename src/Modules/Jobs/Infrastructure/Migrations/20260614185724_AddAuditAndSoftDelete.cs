using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Jobs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedBy",
                schema: "jobs",
                table: "Jobs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                schema: "jobs",
                table: "Jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                schema: "jobs",
                table: "Jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "jobs",
                table: "Jobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                schema: "jobs",
                table: "Jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "jobs",
                table: "Jobs",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                schema: "jobs",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "jobs",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "jobs",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                schema: "jobs",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "jobs",
                table: "Jobs");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedBy",
                schema: "jobs",
                table: "Jobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
