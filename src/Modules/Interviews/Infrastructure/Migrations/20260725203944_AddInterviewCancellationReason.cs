using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Interviews.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewCancellationReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationNote",
                schema: "interviews",
                table: "Interviews",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                schema: "interviews",
                table: "Interviews",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationNote",
                schema: "interviews",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                schema: "interviews",
                table: "Interviews");
        }
    }
}
