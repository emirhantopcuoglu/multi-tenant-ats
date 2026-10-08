using System.Security.Claims;
using Ats.IntegrationTests.Shared;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Microsoft.AspNetCore.Authorization;
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

        // Assert — frozen, but the stamp survives: the session must reach the reactivation screen
        Assert.True(result.IsSuccess);
        var account = await LoadAccountAsync(accountId);
        Assert.Equal(CandidateAccountStatus.Frozen, account.Status);
        Assert.NotNull(account.FrozenAtUtc);
        Assert.True(await RunStampHandlerAsync(accountId, stampBefore));
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
    public async Task Delete_should_reject_a_wrong_password_and_change_nothing()
    {
        // Arrange
        var accountId = await SeedAccountAsync();

        // Act
        var result = await CreateService().DeleteAsync(
            accountId, new DeleteCandidateAccountCommand("wrong-password"));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CandidateAccountLifecycleErrors.InvalidCurrentPassword.Code, result.Error.Code);
        Assert.Equal(CandidateAccountStatus.Active, (await LoadAccountAsync(accountId)).Status);
    }

    [Fact]
    public async Task Delete_should_anonymize_the_row_in_place()
    {
        // Arrange
        var accountId = await SeedAccountAsync();
        await CreateProfileService().UpdateAsync(accountId, new UpdateCandidateProfileCommand(
            "Jane", "Doe", "+905321234567", "Turkey", "Istanbul",
            DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30)));

        // Act
        var result = await CreateService().DeleteAsync(
            accountId, new DeleteCandidateAccountCommand(Password));

        // Assert — read past the query filter: the row still exists, but nothing personal survives
        Assert.True(result.IsSuccess);
        var account = await LoadDeletedAccountAsync(accountId);
        Assert.Equal(CandidateAccountStatus.Deleted, account.Status);
        Assert.NotNull(account.DeletedAtUtc);
        Assert.Equal(CandidateAccount.BuildAnonymizedEmail(accountId), account.Email);
        Assert.Equal(CandidateAccount.AnonymizedFirstName, account.FirstName);
        Assert.Null(account.PhoneNumber);
        Assert.Null(account.Country);
        Assert.Null(account.City);
        Assert.Null(account.BirthDate);
    }

    [Fact]
    public async Task Delete_should_make_login_fail_like_an_unknown_account()
    {
        // Arrange
        var accountId = await SeedAccountAsync();
        await CreateService().DeleteAsync(accountId, new DeleteCandidateAccountCommand(Password));

        // Act — correct credentials, but the query filter hides the row from login
        var login = await CreateAuthService().LoginAsync(Email, Password);

        // Assert — indistinguishable from a typo: deletion must not become an account oracle
        Assert.True(login.IsFailure);
        Assert.Equal(CandidateAuthErrors.InvalidCredentials.Code, login.Error.Code);
    }

    [Fact]
    public async Task Delete_should_kill_every_live_session()
    {
        // Arrange
        var accountId = await SeedAccountAsync();
        var stampBefore = (await LoadAccountAsync(accountId)).SecurityStamp;

        // Act
        await CreateService().DeleteAsync(accountId, new DeleteCandidateAccountCommand(Password));

        // Assert — the filter hides the row from the stamp handler, so any old token dies
        Assert.False(await RunStampHandlerAsync(accountId, stampBefore));
    }

    [Fact]
    public async Task Delete_should_free_the_email_for_a_new_registration()
    {
        // Arrange
        var accountId = await SeedAccountAsync();
        await CreateService().DeleteAsync(accountId, new DeleteCandidateAccountCommand(Password));

        // Act — the locked product decision: deletion is final, the address is reusable
        var register = await CreateAuthService().RegisterAsync(Email, Password, "John", "Roe", SupportedLanguages.Default);

        // Assert
        Assert.True(register.IsSuccess);
    }

    [Fact]
    public async Task Delete_should_remove_the_accounts_email_change_requests()
    {
        // Arrange — a pending request holds a real address, which the erasure must also cover
        var accountId = await SeedAccountAsync();
        var emailSender = new RecordingEmailSender();
        await CreateProfileService(emailSender).RequestEmailChangeAsync(
            accountId, new RequestCandidateEmailChangeCommand("new@example.com", Password));

        // Act
        await CreateService().DeleteAsync(accountId, new DeleteCandidateAccountCommand(Password));

        // Assert
        await using var db = CreateDbContext();
        Assert.False(await db.EmailChangeRequests.AnyAsync(r => r.CandidateAccountId == accountId));
    }

    [Fact]
    public async Task Delete_should_hide_the_account_from_the_profile_read()
    {
        // Arrange
        var accountId = await SeedAccountAsync();
        await CreateService().DeleteAsync(accountId, new DeleteCandidateAccountCommand(Password));

        // Act
        var profile = await CreateProfileService().GetAsync(accountId);

        // Assert
        Assert.True(profile.IsFailure);
        Assert.Equal(CandidateProfileErrors.NotFound.Code, profile.Error.Code);
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
            Secret = "candidate-lifecycle-tests-signing-key",
            Issuer = "ats-tests",
            Audience = "ats-tests",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });

    private static CandidateTokenService CreateTokenService() => new(CreateJwtOptions());

    private CandidateAccountLifecycleService CreateService(
        RecordingFileStorage? fileStorage = null) => new(
        CreateDbContext(),
        CreatePasswordHasher(),
        fileStorage ?? new RecordingFileStorage(),
        NullLogger<CandidateAccountLifecycleService>.Instance);

    // Both helpers hand the session issuer the same DbContext as the service under test, matching how
    // DI scopes them in the app, so a staged revocation and its replacement row commit together.
    private CandidateAuthService CreateAuthService()
    {
        var db = CreateDbContext();
        return new CandidateAuthService(
            db,
            CreatePasswordHasher(),
            new CandidateSessionIssuer(db, CreateTokenService(), CreateJwtOptions()),
            CandidateServiceFactory.EmailVerification(db),
            CandidateServiceFactory.Lockout());
    }

    private CandidateProfileService CreateProfileService(RecordingEmailSender? emailSender = null)
    {
        var db = CreateDbContext();
        return new CandidateProfileService(
            db,
            CreatePasswordHasher(),
            new CandidateSessionIssuer(db, CreateTokenService(), CreateJwtOptions()),
            emailSender ?? new RecordingEmailSender(),
            new JsonEmailTextProvider(),
            new RecordingFileStorage(),
            Options.Create(new CandidateEmailChangeOptions()),
            NullLogger<CandidateProfileService>.Instance);
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

    // Deleted rows are invisible to normal queries by design, so verifying the anonymization needs
    // the one legitimate use of IgnoreQueryFilters in the codebase.
    private async Task<CandidateAccount> LoadDeletedAccountAsync(Guid accountId)
    {
        await using var db = CreateDbContext();
        return await db.CandidateAccounts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(c => c.Id == accountId);
    }
}
