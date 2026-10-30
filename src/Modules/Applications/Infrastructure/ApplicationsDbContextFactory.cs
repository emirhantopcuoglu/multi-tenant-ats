using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ats.Modules.Applications.Infrastructure;

// Used by dotnet ef at design time.
public sealed class ApplicationsDbContextFactory : IDesignTimeDbContextFactory<ApplicationsDbContext>
{
    public ApplicationsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ATS_DB_CONNECTION")
            ?? "Host=localhost;Port=5434;Database=ats;Username=ats;Password=ats_dev_password";

        var options = new DbContextOptionsBuilder<ApplicationsDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "applications"))
            .Options;

        return new ApplicationsDbContext(options, new NullCurrentTenant());
    }

    private sealed class NullCurrentTenant : ICurrentTenant
    {
        public Guid? TenantId => null;
    }
}
