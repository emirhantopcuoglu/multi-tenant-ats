using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Interviews.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "interviews",
                table: "Interviews",
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
                schema: "interviews",
                table: "Interviews");
        }
    }
}
