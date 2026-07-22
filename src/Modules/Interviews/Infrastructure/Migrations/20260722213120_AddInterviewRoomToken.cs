using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Interviews.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewRoomToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Added nullable first: a NOT NULL + unique column can't be added in one step once rows
            // already exist, since every row would collide on the same default value. Existing rows
            // are backfilled with a distinct token (gen_random_uuid() is built into Postgres core
            // since v13, no extension needed) before the column is locked down.
            migrationBuilder.AddColumn<string>(
                name: "RoomToken",
                schema: "interviews",
                table: "Interviews",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE interviews."Interviews"
                SET "RoomToken" = replace(gen_random_uuid()::text, '-', '') || replace(gen_random_uuid()::text, '-', '')
                WHERE "RoomToken" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "RoomToken",
                schema: "interviews",
                table: "Interviews",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_RoomToken",
                schema: "interviews",
                table: "Interviews",
                column: "RoomToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Interviews_RoomToken",
                schema: "interviews",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "RoomToken",
                schema: "interviews",
                table: "Interviews");
        }
    }
}
