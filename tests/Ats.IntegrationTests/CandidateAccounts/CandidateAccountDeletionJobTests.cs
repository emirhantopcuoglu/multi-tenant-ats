using Ats.IntegrationTests.Shared;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ats.IntegrationTests.CandidateAccounts;

[Collection("Integration")]
public sealed class CandidateAccountDeletionJobTests : IAsyncLifetime
{
    private const string Password = "Sup3r-secret";
    private const string Email = "jane@example.com";

    private static readonly DateTime GraceEndedAt = DateTime.UtcNow - CandidateAccount.DeletionGracePeriod;
    private static readonly CandidatePasswordHasher Hasher = new(new PasswordHasher<CandidateAccount>());

    private readonly PostgresContainerFixture _fixture;

    public CandidateAccountDeletionJobTests(PostgresContainerFixture fixture)
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
    public async Task an_account_whose_grace_period_has_ended_should_be_anonymized()
    {
        var accountId = await SeedAccountAsync(Email, account => account.RequestDeletion(GraceEndedAt));

        await CreateJob().EraseDueAccountsAsync();

        var account = await LoadAsync(accountId);
        Assert.Equal(CandidateAccountStatus.Deleted, account.Status);
        Assert.Equal(AnonymizedEmail.For(accountId), account.Email);
        Assert.Equal(CandidateAccount.AnonymizedFirstName, account.FirstName);
        Assert.Null(account.PhoneNumber);
        Assert.Null(account.City);
        Assert.Null(account.BirthDate);
    }

    [Fact]
    public async Task only_accounts_whose_grace_period_has_ended_should_be_touched()
    {
        var waiting = await SeedAccountAsync(
            "waiting@example.com", account => account.RequestDeletion(GraceEndedAt.AddMinutes(5)));
        var cancelled = await SeedAccountAsync("cancelled@example.com", account =>
        {
            account.RequestDeletion(GraceEndedAt);
            account.CancelDeletion(GraceEndedAt);
        });
        var active = await SeedAccountAsync("active@example.com");

        await CreateJob().EraseDueAccountsAsync();

        Assert.Equal(CandidateAccountStatus.PendingDeletion, (await LoadAsync(waiting)).Status);
        Assert.Equal("waiting@example.com", (await LoadAsync(waiting)).Email);
        Assert.Equal(CandidateAccountStatus.Active, (await LoadAsync(cancelled)).Status);
        Assert.Equal(CandidateAccountStatus.Active, (await LoadAsync(active)).Status);
    }

    [Fact]
    public async Task an_erased_account_should_fail_login_like_an_unknown_one()
    {
        await SeedAccountAsync(Email, account => account.RequestDeletion(GraceEndedAt));
        await CreateJob().EraseDueAccountsAsync();

        var login = await CreateAuthService().LoginAsync(Email, Password);

        // Indistinguishable from a typo: deletion must not become an account oracle.
        Assert.True(login.IsFailure);
        Assert.Equal(CandidateAuthErrors.InvalidCredentials.Code, login.Error.Code);
    }

    [Fact]
    public async Task an_erased_account_should_free_its_email_for_a_new_registration()
    {
        await SeedAccountAsync(Email, account => account.RequestDeletion(GraceEndedAt));
        await CreateJob().EraseDueAccountsAsync();

        var register = await CreateAuthService().RegisterAsync(Email, Password, "John", "Roe", SupportedLanguages.Default);

        Assert.True(register.IsSuccess);
    }

    [Fact]
    public async Task an_erased_account_should_be_hidden_from_the_profile_read()
    {
        var accountId = await SeedAccountAsync(Email, account => account.RequestDeletion(GraceEndedAt));
        await CreateJob().EraseDueAccountsAsync();

        var profile = await CreateProfileService().GetAsync(accountId);

        Assert.True(profile.IsFailure);
        Assert.Equal(CandidateProfileErrors.NotFound.Code, profile.Error.Code);
    }

    // A pending email change holds a real address.
    [Fact]
    public async Task erasure_should_remove_the_accounts_email_change_requests()
    {
        var accountId = await SeedAccountAsync(Email);
        await CreateProfileService().RequestEmailChangeAsync(
            accountId, new RequestCandidateEmailChangeCommand("new@example.com", Password));
        await RequestDeletionAsync(accountId);

        await CreateJob().EraseDueAccountsAsync();

        await using var db = CreateDbContext();
        Assert.False(await db.EmailChangeRequests.AnyAsync(r => r.CandidateAccountId == accountId));
    }

    // The orphan file cleanup removes the object later, since nothing points to it anymore.
    [Fact]
    public async Task a_storage_outage_should_not_undo_the_erasure()
    {
        var accountId = await SeedAccountAsync(Email, account =>
        {
            account.AttachCv("candidate-cvs/jane.pdf", "jane.pdf");
            account.RequestDeletion(GraceEndedAt);
        });
        var storage = new RecordingFileStorage { DeleteFailure = new IOException("storage down") };

        await CreateJob(storage).EraseDueAccountsAsync();

        var account = await LoadAsync(accountId);
        Assert.Equal(CandidateAccountStatus.Deleted, account.Status);
        Assert.Null(account.CvFileKey);
    }

    [Fact]
    public async Task one_account_that_cannot_be_erased_should_not_hold_back_the_others()
    {
        var broken = await SeedAccountAsync(
            "broken@example.com", account => account.RequestDeletion(GraceEndedAt.AddMinutes(-1)));
        var healthy = await SeedAccountAsync("healthy@example.com", account => account.RequestDeletion(GraceEndedAt));
        var job = CreateJob(db: CreateDbContext(new FailSavesOf(broken)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.EraseDueAccountsAsync());

        Assert.Equal(CandidateAccountStatus.PendingDeletion, (await LoadAsync(broken)).Status);
        Assert.Equal(CandidateAccountStatus.Deleted, (await LoadAsync(healthy)).Status);
    }

    // The job only picks up pending accounts, so erasing the account first would leave the other
    // modules' data behind for good if they failed.
    [Fact]
    public async Task the_other_modules_should_be_erased_while_the_account_is_still_pending()
    {
        var accountId = await SeedAccountAsync(Email, account => account.RequestDeletion(GraceEndedAt));
        var eraser = new RecordingEraser(LoadAsync);

        await CreateJob(erasers: [eraser]).EraseDueAccountsAsync();

        Assert.Equal([(accountId, CandidateAccountStatus.PendingDeletion)], eraser.Calls);
        Assert.Equal(CandidateAccountStatus.Deleted, (await LoadAsync(accountId)).Status);
    }

    [Fact]
    public async Task an_account_whose_data_could_not_be_erased_should_be_tried_again_on_the_next_run()
    {
        var accountId = await SeedAccountAsync(Email, account => account.RequestDeletion(GraceEndedAt));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateJob(erasers: [new FailingEraser()]).EraseDueAccountsAsync());

        var pending = await LoadAsync(accountId);
        Assert.Equal(CandidateAccountStatus.PendingDeletion, pending.Status);
        Assert.Equal(Email, pending.Email);

        await CreateJob(erasers: [new RecordingEraser(LoadAsync)]).EraseDueAccountsAsync();

        Assert.Equal(CandidateAccountStatus.Deleted, (await LoadAsync(accountId)).Status);
    }

    [Fact]
    public async Task an_account_still_in_its_grace_period_should_not_reach_the_other_modules()
    {
        await SeedAccountAsync(Email, account => account.RequestDeletion(GraceEndedAt.AddMinutes(5)));
        var eraser = new RecordingEraser(LoadAsync);

        await CreateJob(erasers: [eraser]).EraseDueAccountsAsync();

        Assert.Empty(eraser.Calls);
    }

    private CandidateAccountsDbContext CreateDbContext(IInterceptor? interceptor = null)
    {
        var options = PostgresContainerFixture.BuildCandidateAccountsOptions(_fixture.ConnectionString);
        return interceptor is null
            ? new CandidateAccountsDbContext(options)
            : new CandidateAccountsDbContext(
                new DbContextOptionsBuilder<CandidateAccountsDbContext>(options).AddInterceptors(interceptor).Options);
    }

    private CandidateAccountDeletionJob CreateJob(
        RecordingFileStorage? storage = null,
        CandidateAccountsDbContext? db = null,
        IEnumerable<ICandidateDataEraser>? erasers = null) =>
        new(
            db ?? CreateDbContext(), erasers ?? [], storage ?? new RecordingFileStorage(),
            NullLogger<CandidateAccountDeletionJob>.Instance);

    // Records the account's status at the time of the call.
    private sealed class RecordingEraser : ICandidateDataEraser
    {
        private readonly Func<Guid, Task<CandidateAccount>> _load;

        public RecordingEraser(Func<Guid, Task<CandidateAccount>> load) => _load = load;

        public List<(Guid AccountId, CandidateAccountStatus Status)> Calls { get; } = [];

        public async Task EraseAsync(Guid candidateAccountId, CancellationToken cancellationToken = default) =>
            Calls.Add((candidateAccountId, (await _load(candidateAccountId)).Status));
    }

    private sealed class FailingEraser : ICandidateDataEraser
    {
        public Task EraseAsync(Guid candidateAccountId, CancellationToken cancellationToken = default) =>
            throw new IOException("Mongo is down.");
    }

    // A save that changes this account always fails, the way a broken row would.
    private sealed class FailSavesOf : SaveChangesInterceptor
    {
        private readonly Guid _accountId;

        public FailSavesOf(Guid accountId) => _accountId = accountId;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var touchesAccount = eventData.Context!.ChangeTracker.Entries<CandidateAccount>()
                .Any(entry => entry.Entity.Id == _accountId && entry.State == EntityState.Modified);

            return touchesAccount
                ? throw new DbUpdateException("Simulated failure.")
                : ValueTask.FromResult(result);
        }
    }

    private static IOptions<CandidateJwtOptions> JwtOptions =>
        Options.Create(new CandidateJwtOptions
        {
            Secret = "candidate-deletion-job-tests-signing-key",
            Issuer = "ats-tests",
            Audience = "ats-tests",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });

    private CandidateAuthService CreateAuthService()
    {
        var db = CreateDbContext();
        return new CandidateAuthService(
            db,
            Hasher,
            new CandidateSessionIssuer(db, new CandidateTokenService(JwtOptions), JwtOptions),
            CandidateServiceFactory.EmailVerification(db),
            CandidateServiceFactory.Lockout(),
            NullLogger<CandidateAuthService>.Instance);
    }

    private CandidateProfileService CreateProfileService()
    {
        var db = CreateDbContext();
        return new CandidateProfileService(
            db,
            Hasher,
            new CandidateSessionIssuer(db, new CandidateTokenService(JwtOptions), JwtOptions),
            new RecordingEmailSender(),
            new JsonEmailTextProvider(),
            new RecordingFileStorage(),
            Options.Create(new CandidateEmailChangeOptions()),
            NullLogger<CandidateProfileService>.Instance);
    }

    private async Task<Guid> SeedAccountAsync(string email, Action<CandidateAccount>? prepare = null)
    {
        await using var db = CreateDbContext();
        var account = CandidateAccount.Register(email, Hasher.Hash(Password), "Jane", "Doe", SupportedLanguages.Default);
        account.UpdateProfile("Jane", "Doe", "+905321234567", "Turkey", "Istanbul", new DateOnly(1995, 5, 1));
        prepare?.Invoke(account);
        db.CandidateAccounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private async Task RequestDeletionAsync(Guid accountId)
    {
        await using var db = CreateDbContext();
        var account = await db.CandidateAccounts.SingleAsync(c => c.Id == accountId);
        account.RequestDeletion(GraceEndedAt);
        await db.SaveChangesAsync();
    }

    // Erased rows are hidden by the query filter, so checking them has to look past it.
    private async Task<CandidateAccount> LoadAsync(Guid accountId)
    {
        await using var db = CreateDbContext();
        return await db.CandidateAccounts.IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == accountId);
    }
}
