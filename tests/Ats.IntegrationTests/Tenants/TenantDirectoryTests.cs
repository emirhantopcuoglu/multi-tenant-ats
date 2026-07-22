using Ats.IntegrationTests.Shared;
using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ats.IntegrationTests.Tenants;

// Covers the cross-module read the new-application notification fan-out depends on: given a
// tenant id, ITenantDirectory.GetTenantUserIdsAsync must return exactly that tenant's members and
// nobody else's, without relying on ICurrentTenant (a message consumer has no ambient tenant).
[Collection("Integration")]
public sealed class TenantDirectoryTests : IAsyncLifetime
{
    private static readonly string TestPassword = $"Aa1!{Guid.NewGuid():N}";

    private readonly PostgresContainerFixture _fixture;

    public TenantDirectoryTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Start from an empty users/tenants state so one test never sees another's rows.
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tenants.\"AspNetUserRoles\"");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tenants.\"AspNetUsers\"");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tenants.\"Tenants\"");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task should_return_only_the_given_tenants_user_ids()
    {
        // Arrange — two members of the target tenant, one member of another tenant
        var tenantId = await SeedTenantAsync("Acme Inc", "acme-directory");
        var ownerId = await SeedUserAsync(tenantId, "owner@acme.test", "Owner", "One");
        var recruiterId = await SeedUserAsync(tenantId, "recruiter@acme.test", "Rec", "Ruiter");

        var otherTenantId = await SeedTenantAsync("Globex", "globex-directory");
        await SeedUserAsync(otherTenantId, "eve@globex.test", "Eve", "External");

        // Act
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var directory = new TenantDirectory(scope.ServiceProvider.GetRequiredService<TenantsDbContext>());
        var ids = await directory.GetTenantUserIdsAsync(tenantId);

        // Assert
        Assert.Equal(new HashSet<Guid> { ownerId, recruiterId }, ids.ToHashSet());
    }

    [Fact]
    public async Task should_return_empty_for_a_tenant_with_no_users()
    {
        // Act
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var directory = new TenantDirectory(scope.ServiceProvider.GetRequiredService<TenantsDbContext>());
        var ids = await directory.GetTenantUserIdsAsync(Guid.NewGuid());

        // Assert
        Assert.Empty(ids);
    }

    private async Task<Guid> SeedTenantAsync(string companyName, string slug)
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        var tenant = Tenant.Create(companyName, slug);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    private async Task<Guid> SeedUserAsync(Guid tenantId, string email, string firstName, string lastName)
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            TenantId = tenantId,
            CreatedAtUtc = DateTime.UtcNow
        };

        var created = await userManager.CreateAsync(user, TestPassword);
        Assert.True(created.Succeeded);
        return user.Id;
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentTenant>(new FixedTenant(null));
        services.AddDbContext<TenantsDbContext>(options =>
            options.UseNpgsql(_fixture.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants")));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<TenantsDbContext>();
        return services.BuildServiceProvider();
    }
}
