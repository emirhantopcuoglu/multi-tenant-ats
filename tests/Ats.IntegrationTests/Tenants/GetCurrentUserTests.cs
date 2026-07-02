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
public sealed class GetCurrentUserTests : IAsyncLifetime
{
    // Generated at runtime rather than hardcoded so secret scanners don't flag a literal credential.
    // The "Aa1!" prefix guarantees the default Identity password policy (upper, lower, digit,
    // non-alphanumeric, length 6+); the GUID suffix makes each run's password unique and high-entropy.
    private static readonly string TestPassword = $"Aa1!{Guid.NewGuid():N}";

    private readonly PostgresContainerFixture _fixture;

    public GetCurrentUserTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Start each test from an empty users/tenants state so one test never sees another's rows.
        // Delete order respects the FK from AspNetUserRoles -> AspNetUsers.
        await using var provider = BuildProvider(Guid.NewGuid());
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tenants.\"AspNetUserRoles\"");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tenants.\"AspNetUsers\"");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tenants.\"Tenants\"");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task should_return_current_user_with_tenant()
    {
        // Arrange — a tenant with one admin user, created the way registration does.
        var userId = await SeedTenantWithAdminAsync(
            companyName: "Acme Inc", slug: "acme",
            email: "admin@acme.test", firstName: "Ada", lastName: "Admin");

        // Act
        await using var provider = BuildProvider(Guid.NewGuid());
        using var scope = provider.CreateScope();
        var result = await CreateAuthService(scope).GetCurrentUserAsync(userId);

        // Assert
        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Equal(userId, dto.Id);
        Assert.Equal("Ada", dto.FirstName);
        Assert.Equal("Admin", dto.LastName);
        Assert.Equal("admin@acme.test", dto.Email);
        Assert.Equal(Roles.Admin, dto.Role);
        Assert.Equal("Acme Inc", dto.Tenant.CompanyName);
        Assert.Equal("acme", dto.Tenant.Slug);
    }

    [Fact]
    public async Task should_return_failure_when_user_does_not_exist()
    {
        // Arrange — InitializeAsync cleared the tables, so no user has this id.
        await using var provider = BuildProvider(Guid.NewGuid());
        using var scope = provider.CreateScope();

        // Act
        var result = await CreateAuthService(scope).GetCurrentUserAsync(Guid.NewGuid());

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.UserNotFound.Code, result.Error.Code);
    }

    private async Task<Guid> SeedTenantWithAdminAsync(
        string companyName, string slug, string email, string firstName, string lastName)
    {
        await using var provider = BuildProvider(Guid.NewGuid());
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        var tenant = Tenant.Create(companyName, slug);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        if (!await roleManager.RoleExistsAsync(Roles.Admin))
            await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Admin));

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            TenantId = tenant.Id,
            CreatedAtUtc = DateTime.UtcNow
        };

        var created = await userManager.CreateAsync(user, TestPassword);
        Assert.True(created.Succeeded);
        await userManager.AddToRoleAsync(user, Roles.Admin);

        return user.Id;
    }

    // Token service and JwtOptions are unused by GetCurrentUserAsync, so minimal stand-ins suffice.
    private AuthService CreateAuthService(IServiceScope scope) =>
        new(
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<TenantsDbContext>(),
            new StubTokenService(),
            Options.Create(new JwtOptions()),
            scope.ServiceProvider.GetRequiredService<ICurrentTenant>());

    // A real UserManager/RoleManager backed by the container database — the role lookup in
    // GetCurrentUserAsync depends on the Identity join tables, so a faithful test needs the real thing.
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
