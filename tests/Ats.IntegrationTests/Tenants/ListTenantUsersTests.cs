using Ats.IntegrationTests.Shared;
using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ats.IntegrationTests.Tenants;

[Collection("Integration")]
public sealed class ListTenantUsersTests : IAsyncLifetime
{
    private static readonly string TestPassword = $"Aa1!{Guid.NewGuid():N}";

    private readonly PostgresContainerFixture _fixture;

    public ListTenantUsersTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Start from an empty users/tenants state so one test never sees another's rows.
        await using var provider = BuildProvider(null);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tenants.\"AspNetUserRoles\"");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tenants.\"AspNetUsers\"");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tenants.\"Tenants\"");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task should_return_tenant_members_with_roles_ordered_by_name_and_scoped_to_tenant()
    {
        // Arrange — two members in the target tenant, plus a member of another tenant that must not leak.
        var tenantId = await SeedTenantAsync("Acme Inc", "acme");
        await SeedUserAsync(tenantId, "zoe@acme.test", "Zoe", "Zylker", Roles.Recruiter);
        await SeedUserAsync(tenantId, "ada@acme.test", "Ada", "Admin", Roles.Admin);

        var otherTenantId = await SeedTenantAsync("Globex", "globex");
        await SeedUserAsync(otherTenantId, "eve@globex.test", "Eve", "External", Roles.Admin);

        // Act — list members of the first tenant only.
        await using var provider = BuildProvider(tenantId);
        using var scope = provider.CreateScope();
        var users = await CreateAuthService(scope).ListTenantUsersAsync();

        // Assert — both members, ordered by first/last name, with their roles; the other tenant is absent.
        Assert.Equal(2, users.Count);
        Assert.Equal(new[] { "Ada", "Zoe" }, users.Select(u => u.FirstName).ToArray());
        Assert.Equal(Roles.Admin, users[0].Role);
        Assert.Equal(Roles.Recruiter, users[1].Role);
        Assert.DoesNotContain(users, u => u.Email == "eve@globex.test");
    }

    private async Task<Guid> SeedTenantAsync(string companyName, string slug)
    {
        await using var provider = BuildProvider(null);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        var tenant = Tenant.Create(companyName, slug);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    private async Task SeedUserAsync(Guid tenantId, string email, string firstName, string lastName, string role)
    {
        await using var provider = BuildProvider(tenantId);
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));

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
        await userManager.AddToRoleAsync(user, role);
    }

    private AuthService CreateAuthService(IServiceScope scope) =>
        new(
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<TenantsDbContext>(),
            new StubTokenService(),
            Options.Create(new JwtOptions()),
            scope.ServiceProvider.GetRequiredService<ICurrentTenant>());

    private ServiceProvider BuildProvider(Guid? tenantId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentTenant>(new FixedTenant(tenantId));
        services.AddDbContext<TenantsDbContext>(options =>
            options.UseNpgsql(_fixture.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants")));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<TenantsDbContext>();
        return services.BuildServiceProvider();
    }

    private sealed class StubTokenService : ITokenService
    {
        public string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles) => string.Empty;
        public string GenerateRefreshToken() => string.Empty;
    }
}
