using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ats.Modules.Tenants.Infrastructure.Migrations
{
    /// <summary>
    /// Data-only migration, no schema change: EmailConfirmed has always existed on AspNetUsers as part
    /// of ASP.NET Identity — nothing ever wrote or read it, so every row sits at false.
    ///
    /// Company email confirmation now gates login on that column, which would lock out every account
    /// created before this change. They registered when nothing was asked of them, and unlike a
    /// candidate — who can still sign in and fix a wrong address — a company user shut out of login has
    /// no route back at all. So they are grandfathered, and only new registrations face the rule.
    ///
    /// This has to live in a migration rather than a one-off script: CI, the dev database and any
    /// deployment all need it, and a manual step would be remembered in exactly one of them.
    /// </summary>
    public partial class GrandfatherExistingUsersEmailConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE tenants."AspNetUsers"
                SET "EmailConfirmed" = TRUE
                WHERE "EmailConfirmed" = FALSE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately not reversed. Rolling back would have to guess which rows were confirmed by
            // this migration and which by a real click afterwards, and getting that wrong locks people
            // out of their own workspace. The safe inverse of "trust these accounts" is to leave them
            // trusted; the column is meaningless again anyway once the guard is gone.
        }
    }
}
