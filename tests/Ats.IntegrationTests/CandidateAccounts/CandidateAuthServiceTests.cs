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

        return new CandidateAuthService(db, passwordHasher, tokenService);
    }

    private static Guid SubjectOf(string accessToken)
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return Guid.Parse(token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
    }
}
