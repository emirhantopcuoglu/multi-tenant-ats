using Ats.IntegrationTests.Shared;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ats.IntegrationTests.CandidateAccounts;

/* The counters are unit-tested in CandidateLockoutTests; what these pin is that the login path uses
   them, and in the right order.

   Two properties, from two different decisions, easy to conflate:

     - the ANSWER a locked account gives is identical to a wrong password and to an unknown address,
       so the response never confirms an account exists;
     - the ORDER — lockout checked before the password — keeps a locked account from re-locking
       itself on every further guess, which would make the lockout permanent.

   Only Guessing_during_a_lockout_should_not_extend_it fails if the order is swapped. The others hold
   either way, because both paths return the same error — worth knowing, so nobody reads them as
   proof the ordering is covered. */
[Collection("Integration")]
public sealed class CandidateLoginLockoutTests
{
    private const string Password = "correct-horse-battery";
    private const string WrongPassword = "wrong-horse-battery";
    private const int MaxAttempts = 3;

    private readonly PostgresContainerFixture _fixture;

    public CandidateLoginLockoutTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Repeated_wrong_passwords_should_lock_the_account()
    {
        var email = await SeedAsync();

        for (var attempt = 0; attempt < MaxAttempts; attempt += 1)
        {
            var failure = await LoginAsync(email, WrongPassword);
            Assert.True(failure.IsFailure);
        }

        // The correct password is now refused too. That is the point: the guard is on the account,
        // not on the guess.
        var locked = await LoginAsync(email, Password);

        Assert.True(locked.IsFailure);
        Assert.Equal(CandidateAuthErrors.InvalidCredentials.Code, locked.Error.Code);
    }

    [Fact]
    public async Task A_locked_account_should_answer_a_right_password_exactly_like_a_wrong_one()
    {
        // If these two answers ever differ, a locked account becomes an oracle: an attacker learns
        // which password is correct and comes back when the window closes. This is about the error
        // value, not the ordering — see Guessing_during_a_lockout_should_not_extend_it for that.
        var email = await SeedAsync();
        for (var attempt = 0; attempt < MaxAttempts; attempt += 1)
            await LoginAsync(email, WrongPassword);

        var withRightPassword = await LoginAsync(email, Password);
        var withWrongPassword = await LoginAsync(email, WrongPassword);

        Assert.True(withRightPassword.IsFailure);
        Assert.True(withWrongPassword.IsFailure);
        Assert.Equal(withWrongPassword.Error.Code, withRightPassword.Error.Code);
        Assert.Equal(withWrongPassword.Error.Message, withRightPassword.Error.Message);
    }

    [Fact]
    public async Task A_locked_account_should_answer_like_an_address_that_was_never_registered()
    {
        // The other half: the refusal must not confirm the account exists, matching how login already
        // treats "no such email" and "wrong password" as one branch.
        var email = await SeedAsync();
        for (var attempt = 0; attempt < MaxAttempts; attempt += 1)
            await LoginAsync(email, WrongPassword);

        var locked = await LoginAsync(email, Password);
        var unknown = await LoginAsync($"{Guid.NewGuid():N}@acme.test", Password);

        Assert.Equal(unknown.Error.Code, locked.Error.Code);
        Assert.Equal(unknown.Error.Message, locked.Error.Message);
    }

    [Fact]
    public async Task Guessing_during_a_lockout_should_not_extend_it()
    {
        // This is what the check-before-the-password ordering actually buys, and it is the only
        // assertion here that fails if the two are swapped.
        //
        // Verify the password first and a locked account still runs RegisterFailedLogin on every
        // wrong guess. Each new run that reaches the limit pushes LockoutEndsAtUtc further out, so an
        // attacker who keeps hammering keeps the real owner locked out indefinitely — the temporary
        // lockout becomes the permanent denial of service its expiry was designed to prevent.
        var email = await SeedAsync();
        for (var attempt = 0; attempt < MaxAttempts; attempt += 1)
            await LoginAsync(email, WrongPassword);

        var lockedUntil = await LockoutEndAsync(email);
        Assert.NotNull(lockedUntil);

        for (var attempt = 0; attempt < MaxAttempts * 2; attempt += 1)
            await LoginAsync(email, WrongPassword);

        Assert.Equal(lockedUntil, await LockoutEndAsync(email));
    }

    [Fact]
    public async Task A_correct_password_should_clear_the_failure_count()
    {
        // Without this the count is cumulative over the account's lifetime and a candidate who
        // mistypes twice a month is eventually locked out for nothing.
        var email = await SeedAsync();
        await LoginAsync(email, WrongPassword);
        await LoginAsync(email, WrongPassword);

        var success = await LoginAsync(email, Password);
        Assert.True(success.IsSuccess);

        Assert.Equal(0, await FailedCountAsync(email));

        // Two more failures would have tripped the old count; they must not now.
        await LoginAsync(email, WrongPassword);
        await LoginAsync(email, WrongPassword);

        Assert.True((await LoginAsync(email, Password)).IsSuccess);
    }

    [Fact]
    public async Task The_lockout_should_expire_and_let_the_owner_back_in()
    {
        // Simulated by rewinding the stored expiry rather than waiting fifteen minutes. It is the
        // stored timestamp the login path reads, so moving it is the same thing as time passing.
        var email = await SeedAsync();
        for (var attempt = 0; attempt < MaxAttempts; attempt += 1)
            await LoginAsync(email, WrongPassword);

        Assert.True((await LoginAsync(email, Password)).IsFailure);

        await using (var db = NewDb())
        {
            var account = await db.CandidateAccounts.SingleAsync(a => a.Email == email);
            db.Entry(account).Property(nameof(CandidateAccount.LockoutEndsAtUtc)).CurrentValue =
                DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        Assert.True((await LoginAsync(email, Password)).IsSuccess);
    }

    [Fact]
    public async Task Setting_a_new_password_should_end_the_lockout()
    {
        // The way out for a locked-out candidate, who cannot tell from the response why they are
        // refused. Proving mailbox ownership is a stronger signal than waiting the window out.
        var email = await SeedAsync();
        for (var attempt = 0; attempt < MaxAttempts; attempt += 1)
            await LoginAsync(email, WrongPassword);

        const string newPassword = "brand-new-passphrase";
        await using (var db = NewDb())
        {
            var account = await db.CandidateAccounts.SingleAsync(a => a.Email == email);
            account.ChangePassword(CreateHasher().Hash(newPassword));
            await db.SaveChangesAsync();
        }

        Assert.True((await LoginAsync(email, newPassword)).IsSuccess);
    }

    private async Task<string> SeedAsync()
    {
        var email = CandidateAccount.NormalizeEmail($"{Guid.NewGuid():N}@acme.test");
        await using var db = NewDb();
        var account = CandidateAccount.Register(
            email, CreateHasher().Hash(Password), "Ada", "Applicant", "en");
        db.CandidateAccounts.Add(account);
        await db.SaveChangesAsync();
        return email;
    }

    private async Task<DateTime?> LockoutEndAsync(string email)
    {
        await using var db = NewDb();
        return await db.CandidateAccounts
            .Where(a => a.Email == email)
            .Select(a => a.LockoutEndsAtUtc)
            .SingleAsync();
    }

    private async Task<int> FailedCountAsync(string email)
    {
        await using var db = NewDb();
        return await db.CandidateAccounts
            .Where(a => a.Email == email)
            .Select(a => a.FailedLoginCount)
            .SingleAsync();
    }

    private async Task<Result<CandidateAuthResult>> LoginAsync(string email, string password)
    {
        await using var db = NewDb();
        var service = new CandidateAuthService(
            db,
            CreateHasher(),
            new CandidateSessionIssuer(db, new CandidateTokenService(JwtOptions), JwtOptions),
            CandidateServiceFactory.EmailVerification(db),
            CandidateServiceFactory.Lockout(maxFailedAttempts: MaxAttempts, lockoutMinutes: 15));

        return await service.LoginAsync(email, password);
    }

    private static CandidatePasswordHasher CreateHasher() =>
        new(new PasswordHasher<CandidateAccount>());

    private static IOptions<CandidateJwtOptions> JwtOptions =>
        Options.Create(new CandidateJwtOptions
        {
            Secret = "candidate-lockout-tests-signing-secret-that-is-long-enough",
            Issuer = "ats-tests",
            Audience = "ats-tests",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
        });

    private CandidateAccountsDbContext NewDb() =>
        new(PostgresContainerFixture.BuildCandidateAccountsOptions(_fixture.ConnectionString));
}
