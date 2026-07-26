using Ats.IntegrationTests.Shared;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ats.IntegrationTests.CandidateAccounts;

// Covers the path the feature actually exists for: a candidate who registers on a Turkish screen
// must be written to in Turkish, and must keep being written to in Turkish after the process that
// registered them is long gone.
//
// These assert on the mailed body rather than on the stored column alone. A test that only checked
// the column would pass with every consumer still hardcoded to English — which was the bug.
[Collection("Integration")]
public sealed class CandidateLanguageTests
{
    private const string Password = "correct horse battery";

    private readonly PostgresContainerFixture _fixture;

    public CandidateLanguageTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task registering_in_turkish_should_store_turkish_and_mail_in_turkish()
    {
        var mail = new RecordingEmailSender();

        await RegisterAsync("tr-newcomer@acme.test", SupportedLanguages.Turkish, mail);

        Assert.Equal(SupportedLanguages.Turkish, await LanguageOfAsync("tr-newcomer@acme.test"));

        var sent = Assert.Single(mail.Sent);
        Assert.Equal("E-posta adresinizi doğrulayın", sent.Subject);
        Assert.Contains("Merhaba Test,", sent.Body);
    }

    [Fact]
    public async Task registering_without_a_language_should_fall_back_to_english()
    {
        // An API client that never asked for a language has not made a mistake worth refusing an
        // account over, so an unrecognised value settles on English instead of failing.
        var mail = new RecordingEmailSender();

        await RegisterAsync("klingon@acme.test", "tlh", mail);

        Assert.Equal(SupportedLanguages.English, await LanguageOfAsync("klingon@acme.test"));
        Assert.Equal("Verify your email address", Assert.Single(mail.Sent).Subject);
    }

    [Fact]
    public async Task switching_language_should_change_what_later_emails_are_written_in()
    {
        // The reason the language is read live rather than frozen onto an integration event: a
        // candidate who switches to Turkish should get Turkish from the next email onwards, not from
        // the next registration.
        var mail = new RecordingEmailSender();
        var accountId = await RegisterAsync("switcher@acme.test", SupportedLanguages.English, mail);

        var switched = await ProfileService(mail)
            .SetPreferredLanguageAsync(accountId, SupportedLanguages.Turkish);
        Assert.True(switched.IsSuccess);

        await using (var db = NewDb())
        {
            await CandidateServiceFactory.EmailVerification(db, mail).SendAsync(accountId);
        }

        Assert.Equal("Verify your email address", mail.Sent[0].Subject);
        Assert.Equal("E-posta adresinizi doğrulayın", mail.Sent[1].Subject);
    }

    [Fact]
    public async Task an_unsupported_language_should_be_refused_by_the_dedicated_endpoint()
    {
        // Unlike registration, this operation exists only to set the language, so a value outside
        // the catalogue is a client bug — storing English quietly would hide it.
        var mail = new RecordingEmailSender();
        var accountId = await RegisterAsync("picky@acme.test", SupportedLanguages.English, mail);

        var result = await ProfileService(mail).SetPreferredLanguageAsync(accountId, "tlh");

        Assert.True(result.IsFailure);
        Assert.Equal(CandidateProfileErrors.UnsupportedLanguage, result.Error);
        Assert.Equal(SupportedLanguages.English, await LanguageOfAsync("picky@acme.test"));
    }

    [Fact]
    public async Task the_reader_should_answer_english_for_an_address_with_no_account()
    {
        // The Notifications consumers ask by address, and applications submitted before candidate
        // logins existed have no account behind them. That must produce an English email, not a throw.
        await using var db = NewDb();
        var reader = new CandidateAccountReader(db);

        var language = await reader.GetPreferredLanguageByEmailAsync("nobody@acme.test");

        Assert.Equal(SupportedLanguages.English, language);
    }

    [Fact]
    public async Task the_reader_should_match_an_address_regardless_of_case()
    {
        // Addresses arrive on integration events from an apply form; the account stores a normalized
        // copy. A case-sensitive comparison here would silently send English to a Turkish candidate.
        var mail = new RecordingEmailSender();
        await RegisterAsync("MixedCase@Acme.Test", SupportedLanguages.Turkish, mail);

        await using var db = NewDb();
        var language = await new CandidateAccountReader(db)
            .GetPreferredLanguageByEmailAsync("MIXEDCASE@ACME.TEST");

        Assert.Equal(SupportedLanguages.Turkish, language);
    }

    private async Task<Guid> RegisterAsync(string email, string language, RecordingEmailSender mail)
    {
        await using var db = NewDb();
        var authService = new CandidateAuthService(
            db,
            new CandidatePasswordHasher(new PasswordHasher<CandidateAccount>()),
            new CandidateSessionIssuer(db, new CandidateTokenService(JwtOptions), JwtOptions),
            CandidateServiceFactory.EmailVerification(db, mail));

        var result = await authService.RegisterAsync(email, Password, "Test", "Candidate", language);
        Assert.True(result.IsSuccess);

        return await db.CandidateAccounts
            .Where(c => c.Email == CandidateAccount.NormalizeEmail(email))
            .Select(c => c.Id)
            .SingleAsync();
    }

    private async Task<string> LanguageOfAsync(string email)
    {
        await using var db = NewDb();
        return await db.CandidateAccounts
            .AsNoTracking()
            .Where(c => c.Email == CandidateAccount.NormalizeEmail(email))
            .Select(c => c.PreferredLanguage)
            .SingleAsync();
    }

    private CandidateProfileService ProfileService(IEmailSender mail) =>
        new(NewDb(),
            new CandidatePasswordHasher(new PasswordHasher<CandidateAccount>()),
            new CandidateSessionIssuer(NewDb(), new CandidateTokenService(JwtOptions), JwtOptions),
            mail,
            new JsonEmailTextProvider(),
            Options.Create(new CandidateEmailChangeOptions()),
            NullLogger<CandidateProfileService>.Instance);

    private static IOptions<CandidateJwtOptions> JwtOptions =>
        Options.Create(new CandidateJwtOptions
        {
            Secret = "language-tests-secret-key-at-least-32-bytes-long",
            Issuer = "ats-tests",
            Audience = "ats-tests",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });

    private CandidateAccountsDbContext NewDb() =>
        new(PostgresContainerFixture.BuildCandidateAccountsOptions(_fixture.ConnectionString));
}
