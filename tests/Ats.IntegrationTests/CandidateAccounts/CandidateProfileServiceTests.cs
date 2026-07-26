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

    [Fact]
    public async Task RequestEmailChange_should_mail_a_link_to_the_new_address_and_store_only_a_hash()
    {
        // Arrange
        var accountId = await SeedAccountAsync("Old!passw0rd");
        var emailSender = new RecordingEmailSender();

        // Act
        var result = await CreateService(emailSender).RequestEmailChangeAsync(
            accountId, new RequestCandidateEmailChangeCommand("new@example.com", "Old!passw0rd"));

        // Assert — the mail goes to the address being claimed, and the raw token from the link is
        // nowhere in the database (only its hash is)
        Assert.True(result.IsSuccess);
        var sent = Assert.Single(emailSender.Sent);
        Assert.Equal("new@example.com", sent.ToEmail);
        var rawToken = ExtractToken(sent.Body);

        await using var db = CreateDbContext();
        var request = await db.EmailChangeRequests.SingleAsync();
        Assert.Equal("new@example.com", request.NewEmail);
        Assert.NotEqual(rawToken, request.TokenHash);
        Assert.True(request.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task RequestEmailChange_should_reject_a_wrong_current_password()
    {
        // Arrange
        var accountId = await SeedAccountAsync("Old!passw0rd");
        var emailSender = new RecordingEmailSender();

        // Act
        var result = await CreateService(emailSender).RequestEmailChangeAsync(
            accountId, new RequestCandidateEmailChangeCommand("new@example.com", "not-the-password"));

        // Assert — no row, no mail: a stolen token alone must not start the flow
        Assert.True(result.IsFailure);
        Assert.Equal(CandidateProfileErrors.InvalidCurrentPassword.Code, result.Error.Code);
        Assert.Empty(emailSender.Sent);
        await using var db = CreateDbContext();
        Assert.False(await db.EmailChangeRequests.AnyAsync());
    }

    [Theory]
    [InlineData("jane@example.com", "candidate_profile.email_unchanged")]
    [InlineData("not-an-email", "candidate_profile.invalid_email")]
    public async Task RequestEmailChange_should_reject_unusable_addresses(string newEmail, string expectedCode)
    {
        // Arrange
        var accountId = await SeedAccountAsync("Old!passw0rd");

        // Act
        var result = await CreateService().RequestEmailChangeAsync(
            accountId, new RequestCandidateEmailChangeCommand(newEmail, "Old!passw0rd"));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
    }

    [Fact]
    public async Task RequestEmailChange_should_reject_an_email_already_registered_to_another_account()
    {
        // Arrange — a second account already owns the coveted address
        var accountId = await SeedAccountAsync("Old!passw0rd");
        await SeedAccountAsync(email: "taken@example.com");

        // Act
        var result = await CreateService().RequestEmailChangeAsync(
            accountId, new RequestCandidateEmailChangeCommand("taken@example.com", "Old!passw0rd"));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CandidateProfileErrors.EmailAlreadyRegistered.Code, result.Error.Code);
    }

    [Fact]
    public async Task ConfirmEmailChange_should_apply_the_change_and_notify_the_old_address()
    {
        // Arrange — full round trip: request, then confirm with the token from the mailed link
        var accountId = await SeedAccountAsync("Old!passw0rd");
        var stampBefore = (await LoadAccountAsync(accountId)).SecurityStamp;
        var emailSender = new RecordingEmailSender();
        var service = CreateService(emailSender);
        await service.RequestEmailChangeAsync(
            accountId, new RequestCandidateEmailChangeCommand("new@example.com", "Old!passw0rd"));
        var rawToken = ExtractToken(emailSender.Sent.Single().Body);

        // Act
        var result = await service.ConfirmEmailChangeAsync(rawToken);

        // Assert — email swapped, stamp rotated (all sessions dead), old mailbox tipped off
        Assert.True(result.IsSuccess);
        var account = await LoadAccountAsync(accountId);
        Assert.Equal("new@example.com", account.Email);
        Assert.NotEqual(stampBefore, account.SecurityStamp);
        Assert.Contains(emailSender.Sent, mail => mail.ToEmail == "jane@example.com");
    }

    [Fact]
    public async Task ConfirmEmailChange_should_reject_a_reused_token()
    {
        // Arrange
        var accountId = await SeedAccountAsync("Old!passw0rd");
        var emailSender = new RecordingEmailSender();
        var service = CreateService(emailSender);
        await service.RequestEmailChangeAsync(
            accountId, new RequestCandidateEmailChangeCommand("new@example.com", "Old!passw0rd"));
        var rawToken = ExtractToken(emailSender.Sent.Single().Body);
        await service.ConfirmEmailChangeAsync(rawToken);

        // Act — the same link clicked a second time
        var result = await service.ConfirmEmailChangeAsync(rawToken);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CandidateProfileErrors.InvalidEmailChangeToken.Code, result.Error.Code);
    }

    [Fact]
    public async Task ConfirmEmailChange_should_reject_an_unknown_token()
    {
        // Act
        var result = await CreateService().ConfirmEmailChangeAsync("never-issued");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CandidateProfileErrors.InvalidEmailChangeToken.Code, result.Error.Code);
    }

    [Fact]
    public async Task ConfirmEmailChange_should_reject_an_expired_token()
    {
        // Arrange — backdate the expiry directly in the database; the clock cannot be faked here
        var accountId = await SeedAccountAsync("Old!passw0rd");
        var emailSender = new RecordingEmailSender();
        var service = CreateService(emailSender);
        await service.RequestEmailChangeAsync(
            accountId, new RequestCandidateEmailChangeCommand("new@example.com", "Old!passw0rd"));
        var rawToken = ExtractToken(emailSender.Sent.Single().Body);
        await using (var db = CreateDbContext())
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE candidate_accounts.\"EmailChangeRequests\" SET \"ExpiresAtUtc\" = now() at time zone 'utc' - interval '1 minute'");
        }

        // Act — a FRESH service, as in production where request and confirm are separate HTTP
        // requests: the requesting service's change tracker still holds the row with the original
        // expiry and would mask the SQL backdate above.
        var result = await CreateService().ConfirmEmailChangeAsync(rawToken);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CandidateProfileErrors.InvalidEmailChangeToken.Code, result.Error.Code);
    }

    [Fact]
    public async Task ConfirmEmailChange_should_reject_when_the_address_was_registered_meanwhile()
    {
        // Arrange — someone registers the coveted address inside the one-hour window
        var accountId = await SeedAccountAsync("Old!passw0rd");
        var emailSender = new RecordingEmailSender();
        var service = CreateService(emailSender);
        await service.RequestEmailChangeAsync(
            accountId, new RequestCandidateEmailChangeCommand("new@example.com", "Old!passw0rd"));
        var rawToken = ExtractToken(emailSender.Sent.Single().Body);
        await SeedAccountAsync(email: "new@example.com");

        // Act
        var result = await service.ConfirmEmailChangeAsync(rawToken);

        // Assert — the account keeps its old login identity
        Assert.True(result.IsFailure);
        Assert.Equal(CandidateProfileErrors.EmailAlreadyRegistered.Code, result.Error.Code);
        Assert.Equal("jane@example.com", (await LoadAccountAsync(accountId)).Email);
    }

    [Fact]
    public async Task A_second_request_should_supersede_the_first_one()
    {
        // Arrange — the candidate typo'd the address, then requested again with the right one
        var accountId = await SeedAccountAsync("Old!passw0rd");
        var emailSender = new RecordingEmailSender();
        var service = CreateService(emailSender);
        await service.RequestEmailChangeAsync(
            accountId, new RequestCandidateEmailChangeCommand("typo@example.com", "Old!passw0rd"));
        var firstToken = ExtractToken(emailSender.Sent[0].Body);
        await service.RequestEmailChangeAsync(
            accountId, new RequestCandidateEmailChangeCommand("right@example.com", "Old!passw0rd"));
        var secondToken = ExtractToken(emailSender.Sent[1].Body);

        // Act
        var firstResult = await service.ConfirmEmailChangeAsync(firstToken);
        var secondResult = await service.ConfirmEmailChangeAsync(secondToken);

        // Assert — only the latest mailed link may ever work
        Assert.True(firstResult.IsFailure);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal("right@example.com", (await LoadAccountAsync(accountId)).Email);
    }

    // The raw token exists only inside the mailed link; tests recover it the same way a candidate
    // would — by reading the mail.
    private static string ExtractToken(string mailBody)
    {
        var match = System.Text.RegularExpressions.Regex.Match(mailBody, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(match.Success, "The confirmation mail should contain a token link.");
        return match.Groups[1].Value;
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

    private static IOptions<CandidateJwtOptions> CreateJwtOptions() =>
        Options.Create(new CandidateJwtOptions
        {
            Secret = "candidate-profile-tests-signing-key-32b",
            Issuer = "ats-tests",
            Audience = "ats-tests",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });

    private static CandidateTokenService CreateTokenService() => new(CreateJwtOptions());

    private CandidateProfileService CreateService(RecordingEmailSender? emailSender = null)
    {
        // One DbContext shared with the session issuer, matching how DI scopes them in the app: the
        // password change rotates the stamp and re-issues a session, and both writes belong together.
        var db = CreateDbContext();

        return new CandidateProfileService(
            db,
            CreatePasswordHasher(),
            new CandidateSessionIssuer(db, CreateTokenService(), CreateJwtOptions()),
            emailSender ?? new RecordingEmailSender(),
            Options.Create(new CandidateEmailChangeOptions()),
            NullLogger<CandidateProfileService>.Instance);
    }

    private async Task<Guid> SeedAccountAsync(string? password = null, string email = "jane@example.com")
    {
        await using var db = CreateDbContext();
        var passwordHash = password is null ? "hashed-password" : CreatePasswordHasher().Hash(password);
        var account = CandidateAccount.Register(email, passwordHash, "Jane", "Doe");
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
