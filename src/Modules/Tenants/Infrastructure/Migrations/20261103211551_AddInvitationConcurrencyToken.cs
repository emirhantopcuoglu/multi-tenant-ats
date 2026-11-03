using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Tenants.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "tenants",
                table: "Invitations",
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
                schema: "tenants",
                table: "Invitations");
        }
    }
}
