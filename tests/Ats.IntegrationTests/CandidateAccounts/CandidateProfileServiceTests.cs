using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Ats.IntegrationTests.Shared;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ats.IntegrationTests.CandidateAccounts;

// In-memory IEmailSender: unit/integration tests must never talk to a real SMTP server, and the
// profile service only needs "was a mail handed to the port" to be observable.
internal sealed class RecordingEmailSender : IEmailSender
{
    public List<(string ToEmail, string Subject)> Sent { get; } = [];

    public Task SendAsync(
        string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        Sent.Add((toEmail, subject));
        return Task.CompletedTask;
    }
}

// Same hygiene as CandidateAuthServiceTests: CandidateAccount is a global (tenant-less) table, so the
// rows are wiped before each test to keep the runs deterministic.
[Collection("Integration")]
public sealed class CandidateProfileServiceTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;

    public CandidateProfileServiceTests(PostgresContainerFixture fixture)
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
    public async Task Update_should_persist_the_full_profile_and_return_it()
    {
        // Arrange
        var accountId = await SeedAccountAsync();
        var service = CreateService();
        var birthDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30);

        // Act
        var updated = await service.UpdateAsync(accountId, new UpdateCandidateProfileCommand(
            "Janet", "Roe", "+90 532 123 45 67", "Turkey", "Istanbul", birthDate));

        // Assert — the write is visible both in the immediate result and on a fresh read
        Assert.True(updated.IsSuccess);
        Assert.Equal("Janet", updated.Value.FirstName);
        Assert.Equal("+905321234567", updated.Value.PhoneNumber);

        var fresh = await CreateService().GetAsync(accountId);
        Assert.True(fresh.IsSuccess);
        Assert.Equal("Roe", fresh.Value.LastName);
        Assert.Equal("Turkey", fresh.Value.Country);
        Assert.Equal("Istanbul", fresh.Value.City);
        Assert.Equal(birthDate, fresh.Value.BirthDate);
    }

    [Fact]
    public async Task Update_should_fail_for_an_unknown_candidate()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateCandidateProfileCommand(
            "Janet", "Roe", null, null, null, null));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CandidateProfileErrors.NotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Update_should_reject_a_location_outside_the_supported_catalogue()
    {
        // Arrange
        var accountId = await SeedAccountAsync();
        var service = CreateService();

        // Act — a real city, but not one belonging to the selected country
        var result = await service.UpdateAsync(accountId, new UpdateCandidateProfileCommand(
            "Jane", "Doe", null, "Germany", "Istanbul", null));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CandidateProfileErrors.UnsupportedLocation.Code, result.Error.Code);
    }

    [Fact]
    public async Task Update_should_translate_a_domain_invariant_violation_into_a_typed_error()
    {
        // Arrange
        var accountId = await SeedAccountAsync();
        var service = CreateService();

        // Act — an invalid phone trips the domain guard, which must surface as a result, not a throw
        var result = await service.UpdateAsync(accountId, new UpdateCandidateProfileCommand(
            "Jane", "Doe", "not-a-phone", null, null, null));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CandidateProfileErrors.InvalidData("any").Code, result.Error.Code);
    }

    [Fact]
    public async Task ChangePassword_should_replace_the_hash_and_rotate_the_stamp()
    {
        // Arrange
        var accountId = await SeedAccountAsync("Old!passw0rd");
        var stampBefore = (await LoadAccountAsync(accountId)).SecurityStamp;

        // Act
        var result = await CreateService().ChangePasswordAsync(
            accountId, new ChangeCandidatePasswordCommand("Old!passw0rd", "New!passw0rd"));

        // Assert — the new password verifies, the old one no longer does, and the stamp moved
        Assert.True(result.IsSuccess);
        var account = await LoadAccountAsync(accountId);
        var hasher = CreatePasswordHasher();
        Assert.True(hasher.Verify(account.PasswordHash, "New!passw0rd"));
        Assert.False(hasher.Verify(account.PasswordHash, "Old!passw0rd"));
        Assert.NotEqual(stampBefore, account.SecurityStamp);
    }

    [Fact]
    public async Task ChangePassword_should_return_a_token_carrying_the_new_stamp()
    {
        // Arrange
        var accountId = await SeedAccountAsync("Old!passw0rd");

        // Act
        var result = await CreateService().ChangePasswordAsync(
            accountId, new ChangeCandidatePasswordCommand("Old!passw0rd", "New!passw0rd"));

        // Assert — without this the candidate's own session would die with the rotated stamp
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.AccessToken);
        var tokenStamp = token.Claims.Single(c => c.Type == CandidateClaims.SecurityStamp).Value;
        var account = await LoadAccountAsync(accountId);
        Assert.Equal(account.SecurityStamp.ToString(), tokenStamp);
    }

    [Fact]
    public async Task ChangePassword_should_reject_a_wrong_current_password_and_change_nothing()
    {
        // Arrange
        var accountId = await SeedAccountAsync("Old!passw0rd");
        var stampBefore = (await LoadAccountAsync(accountId)).SecurityStamp;

        // Act
        var result = await CreateService().ChangePasswordAsync(
            accountId, new ChangeCandidatePasswordCommand("not-the-password", "New!passw0rd"));

        // Assert — a failed attempt must not log the real owner's sessions out
        Assert.True(result.IsFailure);
        Assert.Equal(CandidateProfileErrors.InvalidCurrentPassword.Code, result.Error.Code);
        var account = await LoadAccountAsync(accountId);
        Assert.True(CreatePasswordHasher().Verify(account.PasswordHash, "Old!passw0rd"));
        Assert.Equal(stampBefore, account.SecurityStamp);
    }

    [Fact]
    public async Task ChangePassword_should_reject_a_new_password_below_the_minimum_length()
    {
        // Arrange
        var accountId = await SeedAccountAsync("Old!passw0rd");

        // Act
        var result = await CreateService().ChangePasswordAsync(
            accountId, new ChangeCandidatePasswordCommand("Old!passw0rd", "short"));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CandidateProfileErrors.PasswordTooShort.Code, result.Error.Code);
    }

    [Fact]
    public async Task ChangePassword_should_send_a_notification_to_the_account_email()
    {
        // Arrange
        var accountId = await SeedAccountAsync("Old!passw0rd");
        var emailSender = new RecordingEmailSender();

        // Act
        var result = await CreateService(emailSender).ChangePasswordAsync(
            accountId, new ChangeCandidatePasswordCommand("Old!passw0rd", "New!passw0rd"));

        // Assert — the owner must learn about a change they did not make
        Assert.True(result.IsSuccess);
        var sent = Assert.Single(emailSender.Sent);
        Assert.Equal("jane@example.com", sent.ToEmail);
    }

    [Fact]
    public async Task Security_stamp_handler_should_reject_tokens_issued_before_a_password_change()
    {
        // Arrange — a principal built from the stamp as it was BEFORE the change, i.e. an old token
        var accountId = await SeedAccountAsync("Old!passw0rd");
        var staleStamp = (await LoadAccountAsync(accountId)).SecurityStamp;
        await CreateService().ChangePasswordAsync(
            accountId, new ChangeCandidatePasswordCommand("Old!passw0rd", "New!passw0rd"));

        // Act
        var staleResult = await RunStampHandlerAsync(accountId, staleStamp);
        var freshStamp = (await LoadAccountAsync(accountId)).SecurityStamp;
        var freshResult = await RunStampHandlerAsync(accountId, freshStamp);

        // Assert — the pre-change token is dead, a token with the current stamp still works
        Assert.False(staleResult);
        Assert.True(freshResult);
    }

    private async Task<bool> RunStampHandlerAsync(Guid accountId, Guid tokenStamp)
    {
        var requirement = new CandidateSecurityStampRequirement();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, accountId.ToString()),
            new Claim(CandidateClaims.SecurityStamp, tokenStamp.ToString())
        ], authenticationType: "Test"));

        var context = new AuthorizationHandlerContext([requirement], principal, resource: null);
        await using var db = CreateDbContext();
        await new CandidateSecurityStampHandler(db).HandleAsync(context);
        return context.HasSucceeded;
    }

    private CandidateAccountsDbContext CreateDbContext() =>
        new(PostgresContainerFixture.BuildCandidateAccountsOptions(_fixture.ConnectionString));

    private static CandidatePasswordHasher CreatePasswordHasher() =>
        new(new PasswordHasher<CandidateAccount>());

    private static CandidateTokenService CreateTokenService() =>
        new(Options.Create(new CandidateJwtOptions
        {
            Secret = "candidate-profile-tests-signing-key-32b",
            Issuer = "ats-tests",
            Audience = "ats-tests",
            AccessTokenMinutes = 15
        }));

    private CandidateProfileService CreateService(RecordingEmailSender? emailSender = null) => new(
        CreateDbContext(),
        CreatePasswordHasher(),
        CreateTokenService(),
        emailSender ?? new RecordingEmailSender(),
        NullLogger<CandidateProfileService>.Instance);

    private async Task<Guid> SeedAccountAsync(string? password = null)
    {
        await using var db = CreateDbContext();
        var passwordHash = password is null ? "hashed-password" : CreatePasswordHasher().Hash(password);
        var account = CandidateAccount.Register("jane@example.com", passwordHash, "Jane", "Doe");
        db.CandidateAccounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private async Task<CandidateAccount> LoadAccountAsync(Guid accountId)
    {
        await using var db = CreateDbContext();
        return await db.CandidateAccounts.AsNoTracking().SingleAsync(c => c.Id == accountId);
    }
}
