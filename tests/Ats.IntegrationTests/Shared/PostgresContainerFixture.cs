using Ats.Modules.Applications.Infrastructure;
using Ats.Modules.Interviews.Infrastructure;
using Ats.Modules.Jobs.Infrastructure;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Ats.IntegrationTests.Shared;

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await ApplyJobsMigrationsAsync();
        await ApplyTenantsMigrationsAsync();
        await ApplyApplicationsMigrationsAsync();
        await ApplyInterviewsMigrationsAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    private async Task ApplyJobsMigrationsAsync()
    {
        var tenant = new FixedTenant(null);
        await using var db = new JobsDbContext(BuildJobsOptions(ConnectionString, tenant), tenant);
        await db.Database.MigrateAsync();
    }

    private async Task ApplyTenantsMigrationsAsync()
    {
        var tenant = new FixedTenant(null);
        await using var db = new TenantsDbContext(BuildTenantsOptions(ConnectionString, tenant), tenant);
        await db.Database.MigrateAsync();
    }

    private async Task ApplyApplicationsMigrationsAsync()
    {
        var tenant = new FixedTenant(null);
        await using var db = new ApplicationsDbContext(BuildApplicationsOptions(ConnectionString, tenant), tenant);
        await db.Database.MigrateAsync();
    }

    private async Task ApplyInterviewsMigrationsAsync()
    {
        var tenant = new FixedTenant(null);
        await using var db = new InterviewsDbContext(BuildInterviewsOptions(ConnectionString, tenant), tenant);
        await db.Database.MigrateAsync();
    }

    internal static DbContextOptions<JobsDbContext> BuildJobsOptions(string connectionString, ICurrentTenant tenant)
        => new DbContextOptionsBuilder<JobsDbContext>()
            .UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "jobs"))
            .AddInterceptors(
                new TenantSaveChangesInterceptor(tenant),
                new AuditableSaveChangesInterceptor(new NullCurrentUser()))
            .Options;

    internal static DbContextOptions<TenantsDbContext> BuildTenantsOptions(string connectionString, ICurrentTenant tenant)
        => new DbContextOptionsBuilder<TenantsDbContext>()
            .UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants"))
            .AddInterceptors(
                new TenantSaveChangesInterceptor(tenant),
                new AuditableSaveChangesInterceptor(new NullCurrentUser()))
            .Options;

    internal static DbContextOptions<ApplicationsDbContext> BuildApplicationsOptions(string connectionString, ICurrentTenant tenant)
        => new DbContextOptionsBuilder<ApplicationsDbContext>()
            .UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "applications"))
            .AddInterceptors(
                new TenantSaveChangesInterceptor(tenant),
                new AuditableSaveChangesInterceptor(new NullCurrentUser()))
            .Options;

    internal static DbContextOptions<InterviewsDbContext> BuildInterviewsOptions(string connectionString, ICurrentTenant tenant)
        => new DbContextOptionsBuilder<InterviewsDbContext>()
            .UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "interviews"))
            .AddInterceptors(
                new TenantSaveChangesInterceptor(tenant),
                new AuditableSaveChangesInterceptor(new NullCurrentUser()))
            .Options;
}
