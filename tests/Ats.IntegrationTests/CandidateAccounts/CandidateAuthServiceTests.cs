using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Ats.IntegrationTests.Shared;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ats.IntegrationTests.CandidateAccounts;

// CandidateAccount is a global (tenant-less) table, so these tests share rows with any other test that
// touches it. The table is wiped before each test to keep uniqueness and counts deterministic.
[Collection("Integration")]
public sealed class CandidateAuthServiceTests : IAsyncLifetime
{
    private const string Secret = "candidate-auth-tests-signing-secret-key-32b";
    private const string Issuer = "ats-tests";
    private const string Audience = "ats-tests";

    private readonly PostgresContainerFixture _fixture;

    public CandidateAuthServiceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var db = new CandidateAccountsDbContext(
            PostgresContainerFixture.BuildCandidateAccountsOptions(_fixture.ConnectionString));
        await db.Database.ExecuteSqlRawAsync("DELETE FROM candidate_accounts.\"CandidateAccounts\"");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Register_then_login_should_succeed_and_me_should_return_the_profile()
    {
        // Arrange
        var service = CreateService();

        // Act — register, then log in with the same credentials
        var register = await service.RegisterAsync("Jane@Example.com", "S3cret!pass", "Jane", "Doe");
        var login = await service.LoginAsync("jane@example.com", "S3cret!pass");

        // Assert — both hand back a token
        Assert.True(register.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(register.Value.AccessToken));
        Assert.True(login.IsSuccess);

        // The "me" read resolves the profile from the token's subject
        var candidateId = SubjectOf(login.Value.AccessToken);
        var me = await service.GetCurrentCandidateAsync(candidateId);

        Assert.True(me.IsSuccess);
        Assert.Equal("jane@example.com", me.Value.Email);
        Assert.Equal("Jane", me.Value.FirstName);
        Assert.Equal("Doe", me.Value.LastName);
    }

    [Fact]
    public async Task Register_should_reject_a_duplicate_email_case_insensitively()
    {
        // Arrange
        var service = CreateService();
        await service.RegisterAsync("jane@example.com", "S3cret!pass", "Jane", "Doe");

        // Act — same email in different casing
        var second = await service.RegisterAsync("JANE@example.com", "Another1!", "Jane", "Roe");

        // Assert
        Assert.True(second.IsFailure);
        Assert.Equal(CandidateAuthErrors.EmailAlreadyRegistered.Code, second.Error.Code);
    }

    [Fact]
    public async Task Login_should_fail_for_a_wrong_password_or_unknown_email()
    {
        // Arrange
        var service = CreateService();
        await service.RegisterAsync("jane@example.com", "S3cret!pass", "Jane", "Doe");

        // Act
        var wrongPassword = await service.LoginAsync("jane@example.com", "not-the-password");
        var unknownEmail = await service.LoginAsync("nobody@example.com", "S3cret!pass");

        // Assert — same opaque error either way, so registration status never leaks
        Assert.Equal(CandidateAuthErrors.InvalidCredentials.Code, wrongPassword.Error.Code);
        Assert.Equal(CandidateAuthErrors.InvalidCredentials.Code, unknownEmail.Error.Code);
    }

    [Fact]
    public async Task Register_should_reject_a_password_below_the_minimum_length()
    {
        // The frontend enforces the same minimum via zod, but that is UX only — the server is the
        // boundary that must hold against a raw HTTP client.
        var service = CreateService();

        var result = await service.RegisterAsync("jane@example.com", "short", "Jane", "Doe");

        Assert.True(result.IsFailure);
        Assert.Equal(CandidateAuthErrors.PasswordTooShort.Code, result.Error.Code);
    }

    [Fact]
    public async Task Issued_token_should_mark_the_candidate_and_carry_no_company_claims()
    {
        // Arrange
        var service = CreateService();

        // Act
        var register = await service.RegisterAsync("jane@example.com", "S3cret!pass", "Jane", "Doe");
        var token = new JwtSecurityTokenHandler().ReadJwtToken(register.Value.AccessToken);

        // Assert — the discriminator that keeps the two identities apart is present...
        Assert.Equal(TokenTypes.Candidate, token.Claims.Single(c => c.Type == TokenTypes.ClaimName).Value);
        // ...and the company-side claims that role/tenant gating relies on are absent
        Assert.DoesNotContain(token.Claims, c => c.Type == "tenant_id");
        Assert.DoesNotContain(token.Claims, c => c.Type == ClaimTypes.Role || c.Type == "role");
    }

    [Fact]
    public async Task Login_should_hand_back_a_refresh_token_alongside_the_access_token()
    {
        var service = CreateService();
        await service.RegisterAsync("jane@example.com", "S3cret!pass", "Jane", "Doe");

        var login = await service.LoginAsync("jane@example.com", "S3cret!pass");

        Assert.True(login.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(login.Value.RefreshToken));
    }

    [Fact]
    public async Task Refresh_should_exchange_a_valid_token_for_a_new_pair()
    {
        // Arrange — a live session
        var register = await CreateService().RegisterAsync("jane@example.com", "S3cret!pass", "Jane", "Doe");

        // Act — redeem the refresh token on a fresh scope, the way the SPA's retry does
        var refresh = await CreateService().RefreshAsync(register.Value.RefreshToken);

        // Assert — a usable pair, and rotated: the refresh half is not the one presented
        Assert.True(refresh.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(refresh.Value.AccessToken));
        Assert.NotEqual(register.Value.RefreshToken, refresh.Value.RefreshToken);
    }

    [Fact]
    public async Task A_refresh_token_should_be_single_use()
    {
        var register = await CreateService().RegisterAsync("jane@example.com", "S3cret!pass", "Jane", "Doe");
        var first = await CreateService().RefreshAsync(register.Value.RefreshToken);

        // Replaying the same token must fail: redemption revoked it and issued a successor.
        var replay = await CreateService().RefreshAsync(register.Value.RefreshToken);

        Assert.True(first.IsSuccess);
        Assert.True(replay.IsFailure);
        Assert.Equal(CandidateAuthErrors.InvalidRefreshToken.Code, replay.Error.Code);
    }

    [Fact]
    public async Task The_rotated_successor_should_itself_be_redeemable()
    {
        // Guards against a rotation that revokes the old token but stores its replacement wrong —
        // which would strand the candidate one refresh later instead of at fifteen minutes.
        var register = await CreateService().RegisterAsync("jane@example.com", "S3cret!pass", "Jane", "Doe");
        var first = await CreateService().RefreshAsync(register.Value.RefreshToken);

        var second = await CreateService().RefreshAsync(first.Value.RefreshToken);

        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Value.RefreshToken, second.Value.RefreshToken);
    }

    [Fact]
    public async Task Refresh_should_fail_for_a_token_issued_before_a_password_change()
    {
        // The reason CandidateRefreshToken carries the security stamp at all. A password change
        // rotates the stamp, which kills live access tokens; if the refresh token survived it, whoever
        // holds it could mint a fresh access token under the NEW stamp and sail straight through the
        // change the owner made to lock them out.
        var register = await CreateService().RegisterAsync("jane@example.com", "S3cret!pass", "Jane", "Doe");
        var accountId = SubjectOf(register.Value.AccessToken);

        await using (var db = new CandidateAccountsDbContext(
            PostgresContainerFixture.BuildCandidateAccountsOptions(_fixture.ConnectionString)))
        {
            var account = await db.CandidateAccounts.SingleAsync(c => c.Id == accountId);
            account.ChangePassword("a-different-hash");
            await db.SaveChangesAsync();
        }

        var refresh = await CreateService().RefreshAsync(register.Value.RefreshToken);

        Assert.True(refresh.IsFailure);
        Assert.Equal(CandidateAuthErrors.InvalidRefreshToken.Code, refresh.Error.Code);
    }

    [Fact]
    public async Task Logout_should_revoke_the_refresh_token()
    {
        var register = await CreateService().RegisterAsync("jane@example.com", "S3cret!pass", "Jane", "Doe");

        await CreateService().LogoutAsync(register.Value.RefreshToken);
        var refresh = await CreateService().RefreshAsync(register.Value.RefreshToken);

        Assert.True(refresh.IsFailure);
        Assert.Equal(CandidateAuthErrors.InvalidRefreshToken.Code, refresh.Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-token")]
    public async Task Refresh_should_fail_for_a_token_that_was_never_issued(string presented)
    {
        var refresh = await CreateService().RefreshAsync(presented);

        Assert.True(refresh.IsFailure);
        Assert.Equal(CandidateAuthErrors.InvalidRefreshToken.Code, refresh.Error.Code);
    }

    [Fact]
    public async Task Logout_should_succeed_for_a_token_that_was_never_issued()
    {
        // Idempotent and silent: logout must not become an oracle for whether a string was a session.
        var result = await CreateService().LogoutAsync("not-a-real-token");

        Assert.True(result.IsSuccess);
    }

    private ICandidateAuthService CreateService()
    {
        var db = new CandidateAccountsDbContext(
            PostgresContainerFixture.BuildCandidateAccountsOptions(_fixture.ConnectionString));

        var passwordHasher = new CandidatePasswordHasher(new PasswordHasher<CandidateAccount>());

        var tokenService = new CandidateTokenService(Options.Create(new CandidateJwtOptions
        {
            Secret = Secret,
            Issuer = Issuer,
            Audience = Audience,
            AccessTokenMinutes = 15
        }));

        // The issuer shares this exact DbContext, not a second one: the refresh path stages a
        // revocation and then has the issuer save it alongside the replacement row, so the two must
        // be in the same change tracker to commit together.
        var sessions = new CandidateSessionIssuer(db, tokenService, CandidateJwtTestOptions);

        return new CandidateAuthService(
            db, passwordHasher, sessions, CandidateServiceFactory.EmailVerification(db));
    }

    private static IOptions<CandidateJwtOptions> CandidateJwtTestOptions =>
        Options.Create(new CandidateJwtOptions
        {
            Secret = Secret,
            Issuer = Issuer,
            Audience = Audience,
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });

    private static Guid SubjectOf(string accessToken)
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return Guid.Parse(token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
    }
}
