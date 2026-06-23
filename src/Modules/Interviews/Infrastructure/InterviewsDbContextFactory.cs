using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ats.Modules.Interviews.Infrastructure;

// Used only by the EF Core CLI (migrations add / database update) at design time. The runtime builds
// the context from DI in Program.cs instead. The tenant is null here because design-time tooling
// never executes tenant-scoped queries.
public sealed class InterviewsDbContextFactory : IDesignTimeDbContextFactory<InterviewsDbContext>
{
    public InterviewsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ATS_DB_CONNECTION")
            ?? "Host=localhost;Port=5434;Database=ats;Username=ats;Password=ats_dev_password";

        var options = new DbContextOptionsBuilder<InterviewsDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "interviews"))
            .Options;

        return new InterviewsDbContext(options, new NullCurrentTenant());
    }

    private sealed class NullCurrentTenant : ICurrentTenant
    {
        public Guid? TenantId => null;
    }
}
