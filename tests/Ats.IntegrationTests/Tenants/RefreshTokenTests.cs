using Ats.IntegrationTests.Shared;
using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ats.IntegrationTests.Tenants;

[Collection("Integration")]
public sealed class RefreshTokenTests
{
    private static readonly string Password = $"Aa1!{Guid.NewGuid():N}";

    private readonly PostgresContainerFixture _fixture;

    public RefreshTokenTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task parallel_refreshes_of_one_token_should_open_one_session()
    {
        await using var provider = BuildProvider();
        var email = await SeedUserAsync(provider);
        var login = await LoginAsync(provider, email);

        var results = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => RefreshAsync(provider, login.RefreshToken)));

        Assert.Single(results, r => r.IsSuccess);
    }

    [Fact]
    public async Task replaying_a_rotated_token_should_end_the_session_it_was_rotated_into()
    {
        await using var provider = BuildProvider();
        var email = await SeedUserAsync(provider);
        var login = await LoginAsync(provider, email);
        var rotated = await RefreshAsync(provider, login.RefreshToken);

        await RefreshAsync(provider, login.RefreshToken);
        var successor = await RefreshAsync(provider, rotated.Value.RefreshToken);

        Assert.True(successor.IsFailure);
    }

    [Fact]
    public async Task a_replay_should_leave_the_users_other_sessions_alone()
    {
        await using var provider = BuildProvider();
        var email = await SeedUserAsync(provider);
        var login = await LoginAsync(provider, email);
        var otherDevice = await LoginAsync(provider, email);
        await RefreshAsync(provider, login.RefreshToken);

        await RefreshAsync(provider, login.RefreshToken);
        var other = await RefreshAsync(provider, otherDevice.RefreshToken);

        Assert.True(other.IsSuccess);
    }

    private static async Task<AuthResult> LoginAsync(ServiceProvider provider, string email)
    {
        using var scope = provider.CreateScope();
        var login = await CreateAuthService(scope).LoginAsync(email, Password);
        Assert.True(login.IsSuccess);
        return login.Value;
    }

    // One scope per call, like one HTTP request.
    private static async Task<Result<AuthResult>> RefreshAsync(ServiceProvider provider, string refreshToken)
    {
        using var scope = provider.CreateScope();
        return await CreateAuthService(scope).RefreshAsync(refreshToken);
    }

    private static async Task<string> SeedUserAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        var tenant = Tenant.Create("Acme", $"acme-{Guid.NewGuid():N}"[..20]);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var email = $"user-{Guid.NewGuid():N}@acme.test";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = "Ada",
            LastName = "Admin",
            TenantId = tenant.Id,
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        var created = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .CreateAsync(user, Password);
        Assert.True(created.Succeeded);
        return email;
    }

    private static AuthService CreateAuthService(IServiceScope scope) =>
        new(
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<TenantsDbContext>(),
            new StubTokenService(),
            Options.Create(new JwtOptions { RefreshTokenDays = 7 }),
            Options.Create(new PasswordResetOptions()),
            Options.Create(new EmailConfirmationOptions()),
            scope.ServiceProvider.GetRequiredService<ICurrentTenant>(),
            new NoOpEmailSender(),
            new JsonEmailTextProvider(),
            NullLogger<AuthService>.Instance);

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

    private sealed class StubTokenService : ITokenService
    {
        public string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles) => string.Empty;
        public string GenerateRefreshToken() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    }
}
