using System.Text.RegularExpressions;
using Ats.IntegrationTests.Shared;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ats.IntegrationTests.CandidateAccounts;

// Same hygiene as the other CandidateAccounts suites: the table is global (tenant-less), so rows are
// wiped before each test to keep runs deterministic.
[Collection("Integration")]
public sealed class CandidatePasswordResetServiceTests : IAsyncLifetime
{
    private const string ResetBaseUrl = "http://localhost:5173/candidate/reset-password";
    private const string Email = "jane@example.com";
    private const string OriginalPassword = "original!pass";

    private readonly PostgresContainerFixture _fixture;

    public CandidatePasswordResetServiceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM candidate_accounts.\"CandidateAccounts\"");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Request_should_mail_a_reset_link_for_a_registered_address()
    {
        // Arrange
        await SeedAccountAsync();
        var mail = new RecordingEmailSender();

        // Act
        var result = await CreateService(mail).RequestAsync(Email);

        // Assert — one mail, to the account, carrying a link to the SPA reset page
        Assert.True(result.IsSuccess);
        var sent = Assert.Single(mail.Sent);
        Assert.Equal(Email, sent.ToEmail);
        Assert.Contains(ResetBaseUrl, sent.Body);
    }

    [Fact]
    public async Task Request_should_report_success_and_send_nothing_for_an_unknown_address()
    {
        // The anti-enumeration rule: the response must not differ from the registered case, or the
        // endpoint becomes a directory of who has an account here.
        var mail = new RecordingEmailSender();

        var result = await CreateService(mail).RequestAsync("nobody@example.com");

        Assert.True(result.IsSuccess);
        Assert.Empty(mail.Sent);
    }

    [Fact]
    public async Task Reset_should_set_the_new_password_and_let_the_candidate_sign_in_with_it()
    {
        await SeedAccountAsync();
        var token = await RequestTokenAsync();

        var reset = await CreateService().ResetAsync(token, "brand!newpass");

        Assert.True(reset.IsSuccess);
        await using var db = CreateDbContext();
        var account = await db.CandidateAccounts.SingleAsync(c => c.Email == Email);
        Assert.True(CreatePasswordHasher().Verify(account.PasswordHash, "brand!newpass"));
        Assert.False(CreatePasswordHasher().Verify(account.PasswordHash, OriginalPassword));
    }

    [Fact]
    public async Task Reset_should_revoke_every_existing_session()
    {
        // The reason a reset is worth anything: if the password was stolen, the thief's live sessions
        // have to die with it. Rotating the stamp is what does that — access tokens fail the
        // per-request stamp check and refresh tokens stop redeeming (see CandidateRefreshToken).
        await SeedAccountAsync();
        var login = await CreateAuthService().LoginAsync(Email, OriginalPassword);
        var stampBefore = await ReadSecurityStampAsync();

        await CreateService().ResetAsync(await RequestTokenAsync(), "brand!newpass");

        Assert.NotEqual(stampBefore, await ReadSecurityStampAsync());
        var refresh = await CreateAuthService().RefreshAsync(login.Value.RefreshToken);
        Assert.True(refresh.IsFailure);
        Assert.Equal(CandidateAuthErrors.InvalidRefreshToken.Code, refresh.Error.Code);
    }

    [Fact]
    public async Task A_reset_token_should_be_single_use()
    {
        await SeedAccountAsync();
        var token = await RequestTokenAsync();

        var first = await CreateService().ResetAsync(token, "brand!newpass");
        var replay = await CreateService().ResetAsync(token, "another!pass");

        Assert.True(first.IsSuccess);
        Assert.True(replay.IsFailure);
        Assert.Equal(CandidatePasswordResetErrors.InvalidToken.Code, replay.Error.Code);
    }

    [Fact]
    public async Task A_newer_request_should_supersede_the_previous_link()
    {
        // Otherwise a forgotten earlier email stays a working key to the account for an hour.
        await SeedAccountAsync();
        var firstToken = await RequestTokenAsync();
        var secondToken = await RequestTokenAsync();

        var stale = await CreateService().ResetAsync(firstToken, "brand!newpass");
        var current = await CreateService().ResetAsync(secondToken, "brand!newpass");

        Assert.True(stale.IsFailure);
        Assert.Equal(CandidatePasswordResetErrors.InvalidToken.Code, stale.Error.Code);
        Assert.True(current.IsSuccess);
    }

    [Fact]
    public async Task Reset_should_fail_for_an_expired_token()
    {
        await SeedAccountAsync();
        var token = await RequestTokenAsync();

        // Age the row past its window rather than waiting an hour. Touching the column directly is
        // the point: the entity has no setter, which is what keeps the expiry honest in production.
        await using (var db = CreateDbContext())
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE candidate_accounts.\"PasswordResetRequests\" SET \"ExpiresAtUtc\" = {0}",
                DateTime.UtcNow.AddMinutes(-1));
        }

        var reset = await CreateService().ResetAsync(token, "brand!newpass");

        Assert.True(reset.IsFailure);
        Assert.Equal(CandidatePasswordResetErrors.InvalidToken.Code, reset.Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("never-issued-token")]
    public async Task Reset_should_fail_for_a_token_that_was_never_issued(string token)
    {
        var reset = await CreateService().ResetAsync(token, "brand!newpass");

        Assert.True(reset.IsFailure);
        Assert.Equal(CandidatePasswordResetErrors.InvalidToken.Code, reset.Error.Code);
    }

    [Fact]
    public async Task Reset_should_reject_a_short_password_without_burning_the_token()
    {
        // Checked before the lookup on purpose: a weak first attempt must not cost the candidate their
        // link and force them back through the mailbox.
        await SeedAccountAsync();
        var token = await RequestTokenAsync();

        var tooShort = await CreateService().ResetAsync(token, "short");
        var retry = await CreateService().ResetAsync(token, "brand!newpass");

        Assert.True(tooShort.IsFailure);
        Assert.Equal(CandidatePasswordResetErrors.PasswordTooShort.Code, tooShort.Error.Code);
        Assert.True(retry.IsSuccess);
    }

    [Fact]
    public async Task Reset_should_notify_the_account_that_the_password_changed()
    {
        // Hijack tripwire: if the owner did not do this, the notice is their signal.
        await SeedAccountAsync();
        var token = await RequestTokenAsync();
        var mail = new RecordingEmailSender();

        await CreateService(mail).ResetAsync(token, "brand!newpass");

        var sent = Assert.Single(mail.Sent);
        Assert.Equal(Email, sent.ToEmail);
        Assert.Contains("reset", sent.Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_frozen_account_should_still_be_able_to_reset()
    {
        // A frozen account can sign in (the SPA routes it to reactivation), so shutting it out of
        // recovery would be a dead end.
        var accountId = await SeedAccountAsync();
        await using (var db = CreateDbContext())
        {
            var account = await db.CandidateAccounts.SingleAsync(c => c.Id == accountId);
            account.Freeze();
            await db.SaveChangesAsync();
        }

        var mail = new RecordingEmailSender();
        await CreateService(mail).RequestAsync(Email);

        Assert.Single(mail.Sent);
    }

    [Fact]
    public async Task A_deleted_account_should_not_receive_a_reset_link()
    {
        // Delete anonymizes the address and the query filter hides the row, so there is nothing to
        // recover and nothing to mail.
        var accountId = await SeedAccountAsync();
        await using (var db = CreateDbContext())
        {
            var account = await db.CandidateAccounts.SingleAsync(c => c.Id == accountId);
            account.Delete();
            await db.SaveChangesAsync();
        }

        var mail = new RecordingEmailSender();
        var result = await CreateService(mail).RequestAsync(Email);

        Assert.True(result.IsSuccess);
        Assert.Empty(mail.Sent);
    }

    // ---- helpers ----

    // Drives the real request path and lifts the raw token back out of the mailed link, so the tests
    // exercise exactly what a candidate would paste — never the stored hash.
    private async Task<string> RequestTokenAsync()
    {
        var mail = new RecordingEmailSender();
        await CreateService(mail).RequestAsync(Email);

        var link = Regex.Match(mail.Sent[^1].Body, @"token=([A-Za-z0-9\-_%]+)");
        Assert.True(link.Success, "The reset email did not contain a token link.");
        return Uri.UnescapeDataString(link.Groups[1].Value);
    }

    private async Task<Guid> SeedAccountAsync()
    {
        await using var db = CreateDbContext();
        var account = CandidateAccount.Register(
            Email, CreatePasswordHasher().Hash(OriginalPassword), "Jane", "Doe");
        db.CandidateAccounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private async Task<Guid> ReadSecurityStampAsync()
    {
        await using var db = CreateDbContext();
        return await db.CandidateAccounts.Where(c => c.Email == Email)
            .Select(c => c.SecurityStamp).SingleAsync();
    }

    private CandidateAccountsDbContext CreateDbContext() =>
        new(PostgresContainerFixture.BuildCandidateAccountsOptions(_fixture.ConnectionString));

    private static CandidatePasswordHasher CreatePasswordHasher() =>
        new(new PasswordHasher<CandidateAccount>());

    private static IOptions<CandidateJwtOptions> CreateJwtOptions() =>
        Options.Create(new CandidateJwtOptions
        {
            Secret = "candidate-password-reset-tests-signing-key",
            Issuer = "ats-tests",
            Audience = "ats-tests",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });

    private CandidatePasswordResetService CreateService(RecordingEmailSender? mail = null) => new(
        CreateDbContext(),
        CreatePasswordHasher(),
        mail ?? new RecordingEmailSender(),
        Options.Create(new CandidatePasswordResetOptions { ResetBaseUrl = ResetBaseUrl }),
        NullLogger<CandidatePasswordResetService>.Instance);

    private CandidateAuthService CreateAuthService()
    {
        var db = CreateDbContext();
        return new CandidateAuthService(
            db,
            CreatePasswordHasher(),
            new CandidateSessionIssuer(db, new CandidateTokenService(CreateJwtOptions()), CreateJwtOptions()),
            CandidateServiceFactory.EmailVerification(db));
    }
}
