using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ats.Modules.Notifications.Infrastructure;

// Used by dotnet ef at design time.
public sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ATS_DB_CONNECTION")
            ?? "Host=localhost;Port=5434;Database=ats;Username=ats;Password=ats_dev_password";

        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications"))
            .Options;

        return new NotificationsDbContext(options);
    }
}
