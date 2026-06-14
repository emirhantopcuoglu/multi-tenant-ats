using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ats.Modules.Tenants.Infrastructure;

public sealed class TenantsDbContextFactory : IDesignTimeDbContextFactory<TenantsDbContext>
{
    public TenantsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ATS_DB_CONNECTION")
            ?? "Host=127.0.0.1;Port=5434;Database=ats;Username=ats;Password=ats_dev_password";

        var options = new DbContextOptionsBuilder<TenantsDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants"))
            .Options;

        return new TenantsDbContext(options, new NullCurrentTenant());
    }
}

internal sealed class NullCurrentTenant : ICurrentTenant
{
    public Guid? TenantId => null;
}
