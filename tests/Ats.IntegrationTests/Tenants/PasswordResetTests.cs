using System.Text.RegularExpressions;
using Ats.IntegrationTests.Shared;
using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ats.Shared.Infrastructure;

namespace Ats.IntegrationTests.Tenants;

[Collection("Integration")]
public sealed class PasswordResetTests : IAsyncLifetime
{
    private const string ResetBaseUrl = "http://localhost:5173/reset-password";
    private const string Email = "admin@acme.test";

    // Generated at runtime rather than hardcoded so secret scanners don't flag a literal credential,
    // matching GetCurrentUserTests. The "Aa1!" prefix satisfies Identity's default password policy.
    private static readonly string OriginalPassword = $"Aa1!{Guid.NewGuid():N}";
    private static readonly string NewPassword = $"Bb2@{Guid.NewGuid():N}";

    private readonly PostgresContainerFixture _fixture;

    public PasswordResetTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tenants.\"RefreshTokens\"");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tenants.\"AspNetUserRoles\"");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tenants.\"AspNetUsers\"");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tenants.\"Tenants\"");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Request_should_mail_a_reset_link_for_a_registered_address()
    {
        // Arrange
        await SeedUserAsync();
        var mail = new RecordingEmailSender();

        // Act
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var result = await CreateAuthService(scope, mail).RequestPasswordResetAsync(Email);

        // Assert — one mail carrying a link to the SPA reset page
        Assert.True(result.IsSuccess);
        var sent = Assert.Single(mail.Sent);
        Assert.Equal(Email, sent.ToEmail);
        Assert.Contains(ResetBaseUrl, sent.Body);
    }

    [Fact]
    public async Task Request_should_report_success_and_send_nothing_for_an_unknown_address()
    {
        // Anti-enumeration: the response must not differ from the registered case, or this endpoint
        // becomes a directory of who works at a company on this platform.
        var mail = new RecordingEmailSender();

        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var result = await CreateAuthService(scope, mail).RequestPasswordResetAsync("nobody@acme.test");

        Assert.True(result.IsSuccess);
        Assert.Empty(mail.Sent);
    }

    [Fact]
    public async Task Reset_should_set_the_new_password()
    {
        await SeedUserAsync();
        var (userId, token) = await RequestResetAsync();

        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var reset = await CreateAuthService(scope).ResetPasswordAsync(userId, token, NewPassword);

        Assert.True(reset.IsSuccess);
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.True(await userManager.CheckPasswordAsync(user!, NewPassword));
        Assert.False(await userManager.CheckPasswordAsync(user!, OriginalPassword));
    }

    [Fact]
    public async Task Reset_should_revoke_the_users_refresh_tokens()
    {
        // The reason this method revokes by hand. Company access tokens carry no security stamp and
        // nothing validates one, so — unlike the candidate side, where rotating the stamp kills
        // sessions automatically — a reset would otherwise leave a stolen refresh token good for its
        // full RefreshTokenDays. Resetting a compromised password has to end the thief's session.
        await SeedUserAsync();
        var login = await LoginAsync();
        var (userId, token) = await RequestResetAsync();

        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        await CreateAuthService(scope).ResetPasswordAsync(userId, token, NewPassword);

        var refresh = await CreateAuthService(scope).RefreshAsync(login.RefreshToken);
        Assert.True(refresh.IsFailure);
        Assert.Equal(AuthErrors.InvalidRefreshToken.Code, refresh.Error.Code);
    }

    [Fact]
    public async Task Reset_should_leave_another_users_refresh_tokens_alone()
    {
        // The revocation is scoped by UserId. A colleague resetting their password must not sign
        // everyone else out.
        await SeedUserAsync();
        await SeedUserAsync("other@acme.test");
        var otherLogin = await LoginAsync("other@acme.test");
        var (userId, token) = await RequestResetAsync();

        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        await CreateAuthService(scope).ResetPasswordAsync(userId, token, NewPassword);

        var refresh = await CreateAuthService(scope).RefreshAsync(otherLogin.RefreshToken);
        Assert.True(refresh.IsSuccess);
    }

    [Fact]
    public async Task A_reset_token_should_be_single_use()
    {
        // Identity's token embeds the security stamp, which ResetPasswordAsync rotates — so the first
        // successful use invalidates the token with no consumed-flag of our own.
        await SeedUserAsync();
        var (userId, token) = await RequestResetAsync();

        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var first = await CreateAuthService(scope).ResetPasswordAsync(userId, token, NewPassword);
        var replay = await CreateAuthService(scope).ResetPasswordAsync(userId, token, $"Cc3#{Guid.NewGuid():N}");

        Assert.True(first.IsSuccess);
        Assert.True(replay.IsFailure);
        Assert.Equal(AuthErrors.InvalidPasswordResetToken.Code, replay.Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("never-issued-token")]
    public async Task Reset_should_fail_for_a_token_that_was_never_issued(string token)
    {
        var userId = await SeedUserAsync();

        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var reset = await CreateAuthService(scope).ResetPasswordAsync(userId, token, NewPassword);

        Assert.True(reset.IsFailure);
        Assert.Equal(AuthErrors.InvalidPasswordResetToken.Code, reset.Error.Code);
    }

    [Fact]
    public async Task Reset_should_fail_for_an_unknown_user()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var reset = await CreateAuthService(scope)
            .ResetPasswordAsync(Guid.NewGuid(), "some-token", NewPassword);

        Assert.True(reset.IsFailure);
        Assert.Equal(AuthErrors.InvalidPasswordResetToken.Code, reset.Error.Code);
    }

    [Fact]
    public async Task A_rejected_password_should_be_reported_separately_from_a_bad_token()
    {
        // These must not answer the same way: a bad link is something the user cannot fix, a password
        // that breaks the policy is. Collapsing both into "invalid link" would send someone hunting
        // for a fresh email that cannot help them.
        await SeedUserAsync();
        var (userId, token) = await RequestResetAsync();

        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var reset = await CreateAuthService(scope).ResetPasswordAsync(userId, token, "short");

        Assert.True(reset.IsFailure);
        Assert.Equal(AuthErrors.PasswordRejected(string.Empty).Code, reset.Error.Code);
    }

    // ---- helpers ----

    // Drives the real request path and lifts the userId/token back out of the mailed link, so the
    // tests exercise exactly what an admin would click.
    private async Task<(Guid UserId, string Token)> RequestResetAsync()
    {
        var mail = new RecordingEmailSender();
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        await CreateAuthService(scope, mail).RequestPasswordResetAsync(Email);

        var body = mail.Sent[^1].Body;
        var userId = Regex.Match(body, @"userId=([^&""]+)");
        var token = Regex.Match(body, @"token=([^&""]+)");
        Assert.True(userId.Success && token.Success, "The reset email did not contain a usable link.");

        return (Guid.Parse(Uri.UnescapeDataString(userId.Groups[1].Value)),
                Uri.UnescapeDataString(token.Groups[1].Value));
    }

    private async Task<AuthResult> LoginAsync(string email = Email)
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var login = await CreateAuthService(scope).LoginAsync(email, OriginalPassword);
        Assert.True(login.IsSuccess);
        return login.Value;
    }

    private async Task<Guid> SeedUserAsync(string email = Email)
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var tenant = await db.Tenants.FirstOrDefaultAsync();
        if (tenant is null)
        {
            tenant = Tenant.Create("Acme Inc", "acme");
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = "Ada",
            LastName = "Admin",
            TenantId = tenant.Id,
            // These suites are about deactivation and password recovery, not email confirmation, so
            // their users are seeded the way a real one looks after confirming. Without it the login
            // guard added with company email confirmation refuses them and every assertion below is
            // about the wrong thing.
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var created = await userManager.CreateAsync(user, OriginalPassword);
        Assert.True(created.Succeeded);
        return user.Id;
    }

    private static AuthService CreateAuthService(
        IServiceScope scope, IEmailSender? mail = null) =>
        new(
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<TenantsDbContext>(),
            new StubTokenService(),
            Options.Create(new JwtOptions { RefreshTokenDays = 7 }),
            Options.Create(new PasswordResetOptions { ResetBaseUrl = ResetBaseUrl }),
            Options.Create(new EmailConfirmationOptions()),
            scope.ServiceProvider.GetRequiredService<ICurrentTenant>(),
            mail ?? new NoOpEmailSender(),
            new JsonEmailTextProvider(),
            NullLogger<AuthService>.Instance);

    // AddDefaultTokenProviders mirrors Program.cs: GeneratePasswordResetTokenAsync throws without a
    // registered "Default" provider, so a test that skipped it would not exercise the real path.
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
        return services.BuildServiceProvider();
    }

    // Refresh tokens are generated by the real service in these tests (LoginAsync must produce a
    // redeemable one), so unlike GetCurrentUserTests' stub this returns real random material.
    private sealed class StubTokenService : ITokenService
    {
        public string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles) => string.Empty;
        public string GenerateRefreshToken() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    }
}
