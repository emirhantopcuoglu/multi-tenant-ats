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

/* The company login form had no account-level guard. The per-IP rate limiter counts requests from an
   address, so it reads a distributed attack on one account as many well-behaved clients — and
   FailOpenRateLimiter deliberately lets everything through when Redis is down, which left the form
   unguarded during an outage.

   Identity has owned the counters all along (AccessFailedCount/LockoutEnd) but enforces them through
   SignInManager, which this codebase does not use: AuthService calls CheckPasswordAsync directly. So
   it now calls AccessFailedAsync/IsLockedOutAsync directly too, and these pin that it does.

   The candidate-side file carries the same argument and the extra assertion that the ordering is
   what stops a locked account re-locking itself; the counters there are ours, here they are
   Identity's, so that behaviour is covered once rather than twice. */
[Collection("Integration")]
public sealed class LoginLockoutTests
{
    private const string Password = "Aa1!aaaaaaaa";
    private const string WrongPassword = "Bb2@bbbbbbbb";
    private const int MaxAttempts = 3;

    private readonly PostgresContainerFixture _fixture;

    public LoginLockoutTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Repeated_wrong_passwords_should_lock_the_account()
    {
        await using var provider = BuildProvider();
        var email = await SeedUserAsync(provider);

        for (var attempt = 0; attempt < MaxAttempts; attempt += 1)
            Assert.True((await LoginAsync(provider, email, WrongPassword)).IsFailure);

        // The correct password is refused too — the guard is on the account, not on the guess.
        var locked = await LoginAsync(provider, email, Password);

        Assert.True(locked.IsFailure);
        Assert.Equal(AuthErrors.InvalidCredentials.Code, locked.Error.Code);
    }

    [Fact]
    public async Task A_locked_account_should_answer_a_right_password_exactly_like_a_wrong_one()
    {
        // If these ever differ, a locked account becomes an oracle: an attacker learns which password
        // is correct and simply comes back when the window closes. This is about the error value the
        // locked branch returns, not about where the check sits.
        await using var provider = BuildProvider();
        var email = await SeedUserAsync(provider);
        for (var attempt = 0; attempt < MaxAttempts; attempt += 1)
            await LoginAsync(provider, email, WrongPassword);

        var withRightPassword = await LoginAsync(provider, email, Password);
        var withWrongPassword = await LoginAsync(provider, email, WrongPassword);

        Assert.True(withRightPassword.IsFailure);
        Assert.Equal(withWrongPassword.Error.Code, withRightPassword.Error.Code);
        Assert.Equal(withWrongPassword.Error.Message, withRightPassword.Error.Message);
    }

    [Fact]
    public async Task A_locked_account_should_answer_like_an_address_that_was_never_registered()
    {
        await using var provider = BuildProvider();
        var email = await SeedUserAsync(provider);
        for (var attempt = 0; attempt < MaxAttempts; attempt += 1)
            await LoginAsync(provider, email, WrongPassword);

        var locked = await LoginAsync(provider, email, Password);
        var unknown = await LoginAsync(provider, $"{Guid.NewGuid():N}@acme.test", Password);

        Assert.Equal(unknown.Error.Code, locked.Error.Code);
        Assert.Equal(unknown.Error.Message, locked.Error.Message);
    }

    [Fact]
    public async Task A_correct_password_should_clear_the_failure_count()
    {
        await using var provider = BuildProvider();
        var email = await SeedUserAsync(provider);
        await LoginAsync(provider, email, WrongPassword);
        await LoginAsync(provider, email, WrongPassword);

        Assert.True((await LoginAsync(provider, email, Password)).IsSuccess);
        Assert.Equal(0, await FailedCountAsync(provider, email));

        // Two more failures must not trip a counter that should have been reset.
        await LoginAsync(provider, email, WrongPassword);
        await LoginAsync(provider, email, WrongPassword);

        Assert.True((await LoginAsync(provider, email, Password)).IsSuccess);
    }

    [Fact]
    public async Task The_lockout_should_expire_and_let_the_owner_back_in()
    {
        // Rewinds the stored expiry rather than waiting: that timestamp is what the login path reads,
        // so moving it is the same thing as time passing.
        await using var provider = BuildProvider();
        var email = await SeedUserAsync(provider);
        for (var attempt = 0; attempt < MaxAttempts; attempt += 1)
            await LoginAsync(provider, email, WrongPassword);

        Assert.True((await LoginAsync(provider, email, Password)).IsFailure);

        using (var scope = provider.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email);
            await users.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddSeconds(-1));
        }

        Assert.True((await LoginAsync(provider, email, Password)).IsSuccess);
    }

    [Fact]
    public async Task Resetting_the_password_should_end_the_lockout()
    {
        // The way out for a locked-out user, who cannot tell from the response why they are refused.
        await using var provider = BuildProvider();
        var email = await SeedUserAsync(provider);
        for (var attempt = 0; attempt < MaxAttempts; attempt += 1)
            await LoginAsync(provider, email, WrongPassword);

        const string newPassword = "Cc3#cccccccc";
        using (var scope = provider.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email);
            var token = await users.GeneratePasswordResetTokenAsync(user!);

            var reset = await CreateAuthService(scope)
                .ResetPasswordAsync(user!.Id, token, newPassword);
            Assert.True(reset.IsSuccess);
        }

        Assert.True((await LoginAsync(provider, email, newPassword)).IsSuccess);
    }

    private async Task<Result<AuthResult>> LoginAsync(
        ServiceProvider provider, string email, string password)
    {
        using var scope = provider.CreateScope();
        return await CreateAuthService(scope).LoginAsync(email, password);
    }

    private static async Task<int> FailedCountAsync(ServiceProvider provider, string email)
    {
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);
        return user!.AccessFailedCount;
    }

    private static async Task<string> SeedUserAsync(ServiceProvider provider)
    {
        var email = $"{Guid.NewGuid():N}@acme.test";
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Ada",
            LastName = "Admin",
            CreatedAtUtc = DateTime.UtcNow,
        };

        var created = await users.CreateAsync(user, Password);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));

        // Identity sets this from Lockout.AllowedForNewUsers, and a false here would make
        // IsLockedOutAsync answer false forever — the counters would tick up against nothing.
        Assert.True(user.LockoutEnabled);

        return email;
    }

    private static AuthService CreateAuthService(IServiceScope scope) =>
        new(
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<TenantsDbContext>(),
            new StubTokenService(),
            Options.Create(new JwtOptions { RefreshTokenDays = 7 }),
            Options.Create(new PasswordResetOptions { ResetBaseUrl = "https://app.test/reset" }),
            Options.Create(new EmailConfirmationOptions()),
            scope.ServiceProvider.GetRequiredService<ICurrentTenant>(),
            new NoOpEmailSender(),
            new JsonEmailTextProvider(),
            NullLogger<AuthService>.Instance);

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<ICurrentTenant>(new FixedTenant(null));
        services.AddDbContext<TenantsDbContext>(options =>
            options.UseNpgsql(_fixture.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants")));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<TenantsDbContext>()
            .AddDefaultTokenProviders();

        // Mirrors Program.cs. Without these the suite would run against Identity's defaults and pass
        // for reasons unrelated to the configuration the application actually ships.
        services.Configure<IdentityOptions>(options =>
        {
            options.Lockout.MaxFailedAccessAttempts = MaxAttempts;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.AllowedForNewUsers = true;
        });

        return services.BuildServiceProvider();
    }

    /* The real token service needs signing configuration this suite has no opinion about; the
       assertions are all about whether login succeeds, never about the token it hands back. */
    private sealed class StubTokenService : ITokenService
    {
        public string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles) => string.Empty;

        public string GenerateRefreshToken() =>
            Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    }
}
