using System.Globalization;
using System.Security.Claims;
using Ats.IntegrationTests.Shared;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;

namespace Ats.IntegrationTests.CandidateAccounts;

[Collection("Integration")]
public sealed class CandidateAccountLifecycleServiceTests : IAsyncLifetime
{
    private const string Password = "Sup3r-secret";
    private const string Email = "jane@example.com";

    private readonly PostgresContainerFixture _fixture;

    public CandidateAccountLifecycleServiceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();
        // Deleting the accounts cascades into EmailChangeRequests, so one statement wipes both.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM candidate_accounts.\"CandidateAccounts\"");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Freeze_should_persist_the_state_and_keep_the_session_alive()
    {
        // Arrange
        var accountId = await SeedAccountAsync();
        var stampBefore = (await LoadAccountAsync(accountId)).SecurityStamp;

        // Act
        var result = await CreateService().FreezeAsync(accountId);

        // Assert: frozen, but the stamp survives: the session must reach the reactivation screen
        Assert.True(result.IsSuccess);
        var account = await LoadAccountAsync(accountId);
        Assert.Equal(CandidateAccountStatus.Frozen, account.Status);
        Assert.NotNull(account.FrozenAtUtc);
        Assert.True(await IsStampCurrentAsync(accountId, stampBefore));
    }

    [Fact]
    public async Task Freeze_should_reject_an_already_frozen_account()
    {
        // Arrange
        var accountId = await SeedAccountAsync();
        await CreateService().FreezeAsync(accountId);

        // Act
        var result = await CreateService().FreezeAsync(accountId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CandidateAccountLifecycleErrors.InvalidState("any").Code, result.Error.Code);
    }

    [Fact]
    public async Task Reactivate_should_restore_a_frozen_account()
    {
        // Arrange
        var accountId = await SeedAccountAsync();
        await CreateService().FreezeAsync(accountId);

        // Act
        var result = await CreateService().ReactivateAsync(accountId);

        // Assert
        Assert.True(result.IsSuccess);
        var account = await LoadAccountAsync(accountId);
        Assert.Equal(CandidateAccountStatus.Active, account.Status);
        Assert.Null(account.FrozenAtUtc);
    }

    [Fact]
    public async Task Reactivate_should_reject_an_active_account()
    {
        // Arrange
        var accountId = await SeedAccountAsync();

        // Act
        var result = await CreateService().ReactivateAsync(accountId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CandidateAccountLifecycleErrors.InvalidState("any").Code, result.Error.Code);
    }

    [Fact]
    public async Task RequestDeletion_should_reject_a_wrong_password_and_change_nothing()
    {
        var accountId = await SeedAccountAsync();
        var mail = new RecordingEmailSender();

        var result = await CreateService(mail).RequestDeletionAsync(
            accountId, new RequestCandidateAccountDeletionCommand("wrong-password"));

        Assert.True(result.IsFailure);
        Assert.Equal(CandidateAccountLifecycleErrors.InvalidCurrentPassword.Code, result.Error.Code);
        Assert.Equal(CandidateAccountStatus.Active, (await LoadAccountAsync(accountId)).Status);
        Assert.Empty(mail.Sent);
    }

    [Fact]
    public async Task RequestDeletion_should_keep_the_data_until_the_returned_due_date()
    {
        var accountId = await SeedAccountAsync();

        var result = await CreateService().RequestDeletionAsync(
            accountId, new RequestCandidateAccountDeletionCommand(Password));

        Assert.True(result.IsSuccess);
        var account = await LoadAccountAsync(accountId);
        Assert.Equal(CandidateAccountStatus.PendingDeletion, account.Status);
        Assert.Equal(account.DeletionDueAtUtc, result.Value);
        Assert.InRange(result.Value, DateTime.UtcNow.AddDays(14).AddMinutes(-1), DateTime.UtcNow.AddDays(14));
        Assert.Equal(Email, account.Email);
        Assert.Equal("Jane", account.FirstName);
    }

    [Fact]
    public async Task RequestDeletion_should_end_every_session()
    {
        var accountId = await SeedAccountAsync();
        var stampBefore = (await LoadAccountAsync(accountId)).SecurityStamp;

        await CreateService().RequestDeletionAsync(accountId, new RequestCandidateAccountDeletionCommand(Password));

        Assert.False(await IsStampCurrentAsync(accountId, stampBefore));
    }

    [Fact]
    public async Task RequestDeletion_should_let_the_candidate_sign_in_to_cancel()
    {
        var accountId = await SeedAccountAsync();
        var requested = await CreateService().RequestDeletionAsync(
            accountId, new RequestCandidateAccountDeletionCommand(Password));

        var login = await CreateAuthService().LoginAsync(Email, Password);
        var current = await CreateAuthService().GetCurrentCandidateAsync(accountId);

        Assert.True(login.IsSuccess);
        Assert.Equal(CandidateAccountStatus.PendingDeletion, current.Value.Status);
        Assert.Equal(requested.Value, current.Value.DeletionDueAtUtc);
    }

    // Tells the owner when the account goes, so a request they did not make can still be stopped.
    [Fact]
    public async Task RequestDeletion_should_mail_the_owner_the_due_date()
    {
        var accountId = await SeedAccountAsync();
        var mail = new RecordingEmailSender();

        var result = await CreateService(mail).RequestDeletionAsync(
            accountId, new RequestCandidateAccountDeletionCommand(Password));

        var sent = Assert.Single(mail.Sent);
        Assert.Equal(Email, sent.ToEmail);
        Assert.Contains(result.Value.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("en")), sent.Body);
    }

    [Fact]
    public async Task RequestDeletion_should_reject_an_account_already_waiting_for_deletion()
    {
        var accountId = await SeedAccountAsync();
        await CreateService().RequestDeletionAsync(accountId, new RequestCandidateAccountDeletionCommand(Password));

        var again = await CreateService().RequestDeletionAsync(
            accountId, new RequestCandidateAccountDeletionCommand(Password));

        Assert.True(again.IsFailure);
        Assert.Equal(CandidateAccountLifecycleErrors.InvalidState("any").Code, again.Error.Code);
    }

    [Fact]
    public async Task CancelDeletion_should_restore_the_account_with_its_data()
    {
        var accountId = await SeedAccountAsync();
        await CreateService().RequestDeletionAsync(accountId, new RequestCandidateAccountDeletionCommand(Password));

        var result = await CreateService().CancelDeletionAsync(accountId);

        Assert.True(result.IsSuccess);
        var account = await LoadAccountAsync(accountId);
        Assert.Equal(CandidateAccountStatus.Active, account.Status);
        Assert.Null(account.DeletionDueAtUtc);
        Assert.Equal(Email, account.Email);
    }

    [Fact]
    public async Task CancelDeletion_should_reject_an_account_without_a_deletion_request()
    {
        var accountId = await SeedAccountAsync();

        var result = await CreateService().CancelDeletionAsync(accountId);

        Assert.True(result.IsFailure);
        Assert.Equal(CandidateAccountLifecycleErrors.InvalidState("any").Code, result.Error.Code);
    }

    private async Task<bool> IsStampCurrentAsync(Guid accountId, Guid tokenStamp)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, accountId.ToString()),
            new Claim(CandidateClaims.SecurityStamp, tokenStamp.ToString())
        ], authenticationType: "Test"));

        await using var db = CreateDbContext();
        return await new CandidateSecurityStampValidator(db).IsCurrentAsync(principal);
    }

    private CandidateAccountsDbContext CreateDbContext() =>
        new(PostgresContainerFixture.BuildCandidateAccountsOptions(_fixture.ConnectionString));

    private static CandidatePasswordHasher CreatePasswordHasher() =>
        new(new PasswordHasher<CandidateAccount>());

    private static IOptions<CandidateJwtOptions> CreateJwtOptions() =>
        Options.Create(new CandidateJwtOptions
        {
            Secret = "candidate-lifecycle-tests-signing-key",
            Issuer = "ats-tests",
            Audience = "ats-tests",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });

    private static CandidateTokenService CreateTokenService() => new(CreateJwtOptions());

    private CandidateAccountLifecycleService CreateService(RecordingEmailSender? emailSender = null) => new(
        CreateDbContext(),
        CreatePasswordHasher(),
        emailSender ?? new RecordingEmailSender(),
        new JsonEmailTextProvider(),
        NullLogger<CandidateAccountLifecycleService>.Instance);

    // Hands the session issuer the same DbContext as the service under test, matching how DI scopes
    // them in the app, so a staged revocation and its replacement row commit together.
    private CandidateAuthService CreateAuthService()
    {
        var db = CreateDbContext();
        return new CandidateAuthService(
            db,
            CreatePasswordHasher(),
            new CandidateSessionIssuer(db, CreateTokenService(), CreateJwtOptions()),
            CandidateServiceFactory.EmailVerification(db),
            CandidateServiceFactory.Lockout(),
            NullLogger<CandidateAuthService>.Instance);
    }

    private async Task<Guid> SeedAccountAsync()
    {
        await using var db = CreateDbContext();
        var account = CandidateAccount.Register(Email, CreatePasswordHasher().Hash(Password), "Jane", "Doe", SupportedLanguages.Default);
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
