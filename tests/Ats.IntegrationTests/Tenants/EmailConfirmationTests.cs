using System.Text.RegularExpressions;
using Ats.IntegrationTests.Shared;
using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ats.IntegrationTests.Tenants;

// Anyone could register a company with an address they did not own; the tenant and its slug were
// created regardless. On this side the session itself waits for the address to be proven, because a
// company user who can sign in can already invite colleagues and publish jobs — there is no single
// consequential action to gate the way "apply" gates a candidate.
//
// Every test drives the token out of the actual mailed link. Identity's confirmation token is
// stateless, so the mail is the only place it exists at all.
[Collection("Integration")]
public sealed class EmailConfirmationTests
{
    // Identity's default password rules are in force (Program.cs configures none), so this needs a
    // digit, both cases and a symbol — the candidate side's length-only policy does not apply here.
    private static readonly string Password = $"Aa1!{Guid.NewGuid():N}";

    private const string ConfirmBaseUrl = "http://localhost:5173/confirm-email";

    private readonly PostgresContainerFixture _fixture;

    public EmailConfirmationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task registration_should_create_the_workspace_but_hand_back_no_session()
    {
        var mail = new RecordingEmailSender();
        await using var provider = BuildProvider();

        var email = NewEmail();
        var result = await RegisterAsync(provider, mail, email, slug: NewSlug());

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code + ": " + result.Error.Message : "");

        // The tenant and the founding Admin exist — registration is not deferred, only the session is.
        Assert.False(await IsConfirmedAsync(provider, email));

        var sent = Assert.Single(mail.Sent);
        Assert.Equal(email, sent.ToEmail);
        Assert.NotNull(TokenFrom(sent.Body));
    }

    [Fact]
    public async Task login_should_be_refused_until_the_address_is_confirmed()
    {
        var mail = new RecordingEmailSender();
        await using var provider = BuildProvider();
        var email = NewEmail();
        await RegisterAsync(provider, mail, email, NewSlug());

        var before = await LoginAsync(provider, email);

        // A distinct code from InvalidCredentials, which is safe because the password already verified
        // — the caller has proved the account is theirs. The UI needs it to offer a resend.
        Assert.True(before.IsFailure);
        Assert.Equal(AuthErrors.EmailNotConfirmed.Code, before.Error.Code);

        await ConfirmFromMailAsync(provider, mail);

        var after = await LoginAsync(provider, email);
        Assert.True(after.IsSuccess);
    }

    [Fact]
    public async Task login_with_a_wrong_password_should_not_reveal_that_confirmation_is_pending()
    {
        // Ordering matters: the confirmation check must sit AFTER the password check. Reversed, this
        // endpoint would tell anyone holding an email address whether it is registered here.
        var mail = new RecordingEmailSender();
        await using var provider = BuildProvider();
        var email = NewEmail();
        await RegisterAsync(provider, mail, email, NewSlug());

        var result = await LoginAsync(provider, email, "not the password");

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.InvalidCredentials.Code, result.Error.Code);
    }

    [Fact]
    public async Task confirming_twice_should_still_report_success()
    {
        // A second click — a duplicate tab, a mail client prefetching links — must not tell someone
        // their working account is broken. Identity's token is stateless, so "already done" is the only
        // replay case that can be distinguished, and it is a success.
        var mail = new RecordingEmailSender();
        await using var provider = BuildProvider();
        await RegisterAsync(provider, mail, NewEmail(), NewSlug());

        Assert.True((await ConfirmFromMailAsync(provider, mail)).IsSuccess);
        Assert.True((await ConfirmFromMailAsync(provider, mail)).IsSuccess);
    }

    [Fact]
    public async Task a_tampered_token_should_be_refused()
    {
        var mail = new RecordingEmailSender();
        await using var provider = BuildProvider();
        var email = NewEmail();
        await RegisterAsync(provider, mail, email, NewSlug());

        var userId = await UserIdOfAsync(provider, email);
        var result = await ConfirmAsync(provider, userId, "not-a-real-token");

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.InvalidEmailConfirmationToken.Code, result.Error.Code);
        Assert.False(await IsConfirmedAsync(provider, email));
    }

    [Fact]
    public async Task a_token_must_not_confirm_a_different_user()
    {
        // Identity binds the token to the user it was minted for. Without that, one confirmation link
        // would confirm any account whose id an attacker could guess.
        var mail = new RecordingEmailSender();
        await using var provider = BuildProvider();

        var victim = NewEmail();
        await RegisterAsync(provider, mail, victim, NewSlug());
        var attackerToken = TokenFrom(mail.Sent[0].Body)!;

        var other = NewEmail();
        await RegisterAsync(provider, mail, other, NewSlug());
        var otherId = await UserIdOfAsync(provider, other);

        var result = await ConfirmAsync(provider, otherId, attackerToken);

        Assert.True(result.IsFailure);
        Assert.False(await IsConfirmedAsync(provider, other));
    }

    [Fact]
    public async Task resending_should_stay_silent_about_unknown_addresses()
    {
        // Anonymous by necessity — whoever needs it cannot sign in — so it must not become a directory
        // of who works here. Success and no mail, for both an unknown and an already-confirmed address.
        var mail = new RecordingEmailSender();
        await using var provider = BuildProvider();

        var unknown = await ResendAsync(provider, mail, "nobody@nowhere.test");
        Assert.True(unknown.IsSuccess);
        Assert.Empty(mail.Sent);

        var email = NewEmail();
        await RegisterAsync(provider, mail, email, NewSlug());
        await ConfirmFromMailAsync(provider, mail);
        var sentSoFar = mail.Sent.Count;

        var alreadyDone = await ResendAsync(provider, mail, email);
        Assert.True(alreadyDone.IsSuccess);
        Assert.Equal(sentSoFar, mail.Sent.Count);
    }

    [Fact]
    public async Task a_resent_link_should_work()
    {
        var mail = new RecordingEmailSender();
        await using var provider = BuildProvider();
        var email = NewEmail();
        await RegisterAsync(provider, mail, email, NewSlug());

        await ResendAsync(provider, mail, email);
        Assert.Equal(2, mail.Sent.Count);

        var result = await ConfirmAsync(
            provider, await UserIdOfAsync(provider, email), TokenFrom(mail.Sent[1].Body)!);

        Assert.True(result.IsSuccess);
        Assert.True(await IsConfirmedAsync(provider, email));
    }

    [Fact]
    public async Task an_invited_colleague_should_be_able_to_sign_in_without_confirming()
    {
        // The invitation link was mailed to that exact address, so reaching acceptance already proves
        // the mailbox is readable. Demanding a second proof would gate someone out of the workspace
        // they were just invited to.
        //
        // Driven end to end through the real InviteAsync -> AcceptAsync -> LoginAsync path. An earlier
        // version of this test created the user itself with EmailConfirmed = true and then asserted the
        // login worked — which proved only that the test's own setup was consistent. Removing the flag
        // from InvitationService broke nothing.
        var mail = new RecordingEmailSender();
        await using var provider = BuildProvider();
        await EnsureAdminRoleAsync(provider);

        var tenantId = await SeedTenantAsync();
        var email = $"colleague-{Guid.NewGuid():N}@acme.test";

        using (var scope = provider.CreateScope())
        {
            var invitations = CreateInvitationService(scope, tenantId, mail);
            Assert.True((await invitations.InviteAsync(email, Roles.Admin)).IsSuccess);
        }

        var invitationToken = TokenFrom(mail.Sent[^1].Body)!;

        using (var scope = provider.CreateScope())
        {
            var invitations = CreateInvitationService(scope, tenantId, mail);
            var accepted = await invitations.AcceptAsync(invitationToken, Password, "New", "Colleague");
            Assert.True(accepted.IsSuccess, accepted.IsFailure ? accepted.Error.Message : "");
        }

        Assert.True(await IsConfirmedAsync(provider, email));

        var login = await LoginAsync(provider, email);
        Assert.True(login.IsSuccess, login.IsFailure ? login.Error.Code : "");
    }

    [Fact]
    public async Task a_failed_registration_should_not_leave_the_slug_taken()
    {
        // The tenant used to be committed before the user existed, so a rejected password left an
        // orphan tenant holding the slug — permanently unregisterable by anyone, including the person
        // who had just failed.
        await using var provider = BuildProvider();
        var mail = new RecordingEmailSender();
        var slug = NewSlug();

        var rejected = await RegisterAsync(provider, mail, NewEmail(), slug, password: "short");
        Assert.True(rejected.IsFailure);

        var retry = await RegisterAsync(provider, mail, NewEmail(), slug);

        Assert.True(retry.IsSuccess);
        await using var db = NewDb();
        Assert.Equal(1, await db.Tenants.IgnoreQueryFilters().CountAsync(t => t.Slug == slug));
    }

    // Pulls the token straight out of the ?token= query parameter of the mailed link.
    private static string? TokenFrom(string body)
    {
        var match = Regex.Match(body, @"[?&]token=([^""&\s]+)");
        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : null;
    }

    private static string NewEmail() => $"founder-{Guid.NewGuid():N}@acme.test";

    // Slugs are unique per tenant and this suite registers many; a fresh one per call keeps the tests
    // independent of each other's leftovers.
    private static string NewSlug() => $"acme-{Guid.NewGuid():N}"[..20];

    // Every helper below awaits INSIDE the scope. Returning the Task and letting `using` dispose the
    // scope first tears down the DbContext while the async work is still running — the whole suite
    // failed with ObjectDisposedException before this was fixed.
    private static async Task<Result> RegisterAsync(
        ServiceProvider provider, IEmailSender mail, string email, string slug, string? password = null)
    {
        await EnsureAdminRoleAsync(provider);

        using var scope = provider.CreateScope();
        return await CreateAuthService(scope, mail).RegisterAsync(
            "Acme", slug, email, password ?? Password, "Ada", "Founder");
    }

    // The app seeds roles at startup; a bare test ServiceProvider does not, and RegisterAsync assigns
    // the founder to Admin — without this every registration fails with "Role ADMIN does not exist".
    // Idempotent, and funnelled through the one helper that needs it.
    private static async Task EnsureAdminRoleAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(Roles.Admin))
            await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Admin));
    }

    private static async Task<Result<AuthResult>> LoginAsync(
        ServiceProvider provider, string email, string? password = null)
    {
        using var scope = provider.CreateScope();
        return await CreateAuthService(scope).LoginAsync(email, password ?? Password);
    }

    private static async Task<Result> ConfirmAsync(ServiceProvider provider, Guid userId, string token)
    {
        using var scope = provider.CreateScope();
        return await CreateAuthService(scope).ConfirmEmailAsync(userId, token);
    }

    private static async Task<Result> ResendAsync(
        ServiceProvider provider, IEmailSender mail, string email)
    {
        using var scope = provider.CreateScope();
        return await CreateAuthService(scope, mail).ResendEmailConfirmationAsync(email);
    }

    // Confirms using the most recently mailed link, which is how a real founder does it.
    private static async Task<Result> ConfirmFromMailAsync(
        ServiceProvider provider, RecordingEmailSender mail)
    {
        var latest = mail.Sent[^1];
        var userId = await UserIdOfAsync(provider, latest.ToEmail);
        return await ConfirmAsync(provider, userId, TokenFrom(latest.Body)!);
    }

    private static async Task<Guid> UserIdOfAsync(ServiceProvider provider, string email)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
    }

    private static async Task<bool> IsConfirmedAsync(ServiceProvider provider, string email)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.EmailConfirmed).SingleAsync();
    }

    // Invitations are tenant-scoped, so this one gets a tenant in scope — unlike the auth service,
    // whose endpoints are all anonymous and therefore run with none.
    private static InvitationService CreateInvitationService(
        IServiceScope scope, Guid tenantId, IEmailSender mail) =>
        new(
            new TenantsDbContext(
                PostgresContainerFixture.BuildTenantsOptions(
                    scope.ServiceProvider.GetRequiredService<TenantsDbContext>().Database.GetConnectionString()!,
                    new FixedTenant(tenantId)),
                new FixedTenant(tenantId)),
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            mail,
            Options.Create(new InvitationOptions()));

    private async Task<Guid> SeedTenantAsync()
    {
        await using var db = NewDb();
        var tenant = Tenant.Create("Acme", NewSlug());
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    private static AuthService CreateAuthService(IServiceScope scope, IEmailSender? mail = null) =>
        new(
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<TenantsDbContext>(),
            new StubTokenService(),
            Options.Create(new JwtOptions { RefreshTokenDays = 7 }),
            Options.Create(new PasswordResetOptions()),
            Options.Create(new EmailConfirmationOptions { ConfirmBaseUrl = ConfirmBaseUrl }),
            scope.ServiceProvider.GetRequiredService<ICurrentTenant>(),
            mail ?? new NoOpEmailSender(),
            NullLogger<AuthService>.Instance);

    // Mirrors Program.cs, including the dedicated confirmation provider: without registering it and
    // pointing Tokens.EmailConfirmationTokenProvider at it, these tests would exercise the shared
    // "Default" provider instead of the one production actually uses.
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
            .AddDefaultTokenProviders()
            .AddTokenProvider<EmailConfirmationTokenProvider<ApplicationUser>>(
                EmailConfirmationTokenProviderOptions.ProviderName);
        services.Configure<IdentityOptions>(options =>
            options.Tokens.EmailConfirmationTokenProvider =
                EmailConfirmationTokenProviderOptions.ProviderName);
        return services.BuildServiceProvider();
    }

    private TenantsDbContext NewDb() =>
        new(PostgresContainerFixture.BuildTenantsOptions(_fixture.ConnectionString, new FixedTenant(null)),
            new FixedTenant(null));

    private sealed class StubTokenService : ITokenService
    {
        public string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles) => string.Empty;
        public string GenerateRefreshToken() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    }
}
