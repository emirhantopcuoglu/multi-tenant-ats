using System.Text.RegularExpressions;
using Ats.IntegrationTests.Shared;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ats.IntegrationTests.CandidateAccounts;

// Every test here drives the token out of the actual mailed link rather than reading it from the
// database. The link is the only place the raw token exists — the row stores a hash — so extracting it
// from the email is both how a candidate really uses this and the only way to prove the hash on the
// row matches what was sent. A test that read the row would pass even if the mail carried garbage.
[Collection("Integration")]
public sealed class CandidateEmailVerificationServiceTests
{
    private readonly PostgresContainerFixture _fixture;

    public CandidateEmailVerificationServiceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task registration_should_leave_the_account_unverified_and_mail_a_link()
    {
        var mail = new RecordingEmailSender();
        var accountId = await RegisterAsync("newcomer@acme.test", mail);

        // Unverified, but a session was still issued — see CandidateAuthService.RegisterAsync for why
        // registration does not gate on this.
        Assert.False(await IsVerifiedAsync(accountId));

        var sent = Assert.Single(mail.Sent);
        Assert.Equal("newcomer@acme.test", sent.ToEmail);
        Assert.Contains("Verify", sent.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(TokenFrom(sent.Body));
    }

    [Fact]
    public async Task the_mailed_link_should_verify_the_account()
    {
        var mail = new RecordingEmailSender();
        var accountId = await RegisterAsync("clicker@acme.test", mail);

        var result = await ConfirmAsync(TokenFrom(mail.Sent[0].Body)!);

        Assert.True(result.IsSuccess);
        Assert.True(await IsVerifiedAsync(accountId));
    }

    [Fact]
    public async Task confirming_should_not_end_the_candidates_session()
    {
        // The candidate is usually verifying from the very tab they registered in. Rotating the
        // security stamp — as a password or email change does — would log them out at their next
        // request, for no security gain: no credential changed here.
        var mail = new RecordingEmailSender();
        var accountId = await RegisterAsync("stayput@acme.test", mail);
        var stampBefore = await SecurityStampOfAsync(accountId);

        await ConfirmAsync(TokenFrom(mail.Sent[0].Body)!);

        Assert.Equal(stampBefore, await SecurityStampOfAsync(accountId));
    }

    [Fact]
    public async Task a_link_should_only_work_once()
    {
        var mail = new RecordingEmailSender();
        await RegisterAsync("twice@acme.test", mail);
        var token = TokenFrom(mail.Sent[0].Body)!;

        Assert.True((await ConfirmAsync(token)).IsSuccess);

        // Replaying a spent link must fail even though the address is already verified: a token that
        // has served its purpose stays dead, and the answer is the same as for one that never existed.
        var replay = await ConfirmAsync(token);
        Assert.True(replay.IsFailure);
        Assert.Equal(CandidateEmailVerificationErrors.InvalidToken.Code, replay.Error.Code);
    }

    [Fact]
    public async Task resending_should_kill_the_previous_link()
    {
        // Otherwise an old email left sitting in an inbox stays redeemable indefinitely, which is the
        // whole reason SendAsync supersedes pending rows instead of adding another.
        var mail = new RecordingEmailSender();
        var accountId = await RegisterAsync("resend@acme.test", mail);
        var firstToken = TokenFrom(mail.Sent[0].Body)!;

        await SendAsync(accountId, mail);
        var secondToken = TokenFrom(mail.Sent[1].Body)!;
        Assert.NotEqual(firstToken, secondToken);

        var stale = await ConfirmAsync(firstToken);
        Assert.True(stale.IsFailure);
        Assert.Equal(CandidateEmailVerificationErrors.InvalidToken.Code, stale.Error.Code);

        Assert.True((await ConfirmAsync(secondToken)).IsSuccess);
    }

    [Fact]
    public async Task an_expired_link_should_be_refused()
    {
        var mail = new RecordingEmailSender();
        var accountId = await RegisterAsync("expired@acme.test", mail);
        var token = TokenFrom(mail.Sent[0].Body)!;

        // Pushed past the window directly: waiting 24 hours is not a test.
        await using (var db = NewDb())
        {
            var request = await db.EmailVerificationRequests
                .SingleAsync(r => r.CandidateAccountId == accountId);
            db.Entry(request).Property(nameof(EmailVerificationRequest.ExpiresAtUtc)).CurrentValue =
                DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var result = await ConfirmAsync(token);

        Assert.True(result.IsFailure);
        Assert.Equal(CandidateEmailVerificationErrors.InvalidToken.Code, result.Error.Code);
        Assert.False(await IsVerifiedAsync(accountId));
    }

    [Fact]
    public async Task an_unknown_token_should_be_refused()
    {
        var result = await ConfirmAsync("not-a-real-token");

        Assert.True(result.IsFailure);
        Assert.Equal(CandidateEmailVerificationErrors.InvalidToken.Code, result.Error.Code);
    }

    [Fact]
    public async Task resending_to_an_already_verified_account_should_say_so()
    {
        // A silent success would leave the UI claiming a link was sent and the candidate waiting for
        // an email that is never coming. The caller here is the signed-in owner, so unlike
        // forgot-password there is nothing to hide from them.
        var mail = new RecordingEmailSender();
        var accountId = await RegisterAsync("done@acme.test", mail);
        await ConfirmAsync(TokenFrom(mail.Sent[0].Body)!);

        var mailCountBefore = mail.Sent.Count;
        var result = await SendAsync(accountId, mail);

        Assert.True(result.IsFailure);
        Assert.Equal(CandidateEmailVerificationErrors.AlreadyVerified.Code, result.Error.Code);
        Assert.Equal(mailCountBefore, mail.Sent.Count);
    }

    // Pulls the token straight out of the ?token= query parameter of the mailed link.
    private static string? TokenFrom(string body)
    {
        var match = Regex.Match(body, @"[?&]token=([^""&\s]+)");
        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : null;
    }

    private async Task<Guid> RegisterAsync(string email, RecordingEmailSender mail)
    {
        await using var db = NewDb();
        var authService = new CandidateAuthService(
            db,
            new CandidatePasswordHasher(
                new Microsoft.AspNetCore.Identity.PasswordHasher<CandidateAccount>()),
            new CandidateSessionIssuer(db, new CandidateTokenService(JwtOptions), JwtOptions),
            CandidateServiceFactory.EmailVerification(db, mail));

        var result = await authService.RegisterAsync(email, "correct horse battery", "Test", "Candidate");
        Assert.True(result.IsSuccess);

        return await db.CandidateAccounts
            .Where(c => c.Email == CandidateAccount.NormalizeEmail(email))
            .Select(c => c.Id)
            .SingleAsync();
    }

    private async Task<Ats.Shared.Kernel.Result> SendAsync(Guid accountId, RecordingEmailSender mail)
    {
        await using var db = NewDb();
        return await CandidateServiceFactory.EmailVerification(db, mail).SendAsync(accountId);
    }

    private async Task<Ats.Shared.Kernel.Result> ConfirmAsync(string token)
    {
        await using var db = NewDb();
        return await CandidateServiceFactory.EmailVerification(db).ConfirmAsync(token);
    }

    private async Task<bool> IsVerifiedAsync(Guid accountId)
    {
        await using var db = NewDb();
        return await db.CandidateAccounts
            .AsNoTracking()
            .Where(c => c.Id == accountId)
            .Select(c => c.EmailVerifiedAtUtc != null)
            .SingleAsync();
    }

    private async Task<Guid> SecurityStampOfAsync(Guid accountId)
    {
        await using var db = NewDb();
        return await db.CandidateAccounts
            .AsNoTracking()
            .Where(c => c.Id == accountId)
            .Select(c => c.SecurityStamp)
            .SingleAsync();
    }

    private static Microsoft.Extensions.Options.IOptions<CandidateJwtOptions> JwtOptions =>
        Microsoft.Extensions.Options.Options.Create(new CandidateJwtOptions
        {
            Secret = "verification-tests-secret-key-at-least-32-bytes-long",
            Issuer = "ats-tests",
            Audience = "ats-tests",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });

    private CandidateAccountsDbContext NewDb() =>
        new(PostgresContainerFixture.BuildCandidateAccountsOptions(_fixture.ConnectionString));
}
