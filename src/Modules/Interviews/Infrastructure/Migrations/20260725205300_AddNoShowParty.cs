using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Interviews.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNoShowParty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NoShowParty",
                schema: "interviews",
                table: "Interviews",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NoShowParty",
                schema: "interviews",
                table: "Interviews");
        }
    }
}
