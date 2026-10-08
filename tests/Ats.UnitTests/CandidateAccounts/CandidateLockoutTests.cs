using Ats.Modules.CandidateAccounts.Domain;

namespace Ats.UnitTests.CandidateAccounts;

// The counters behind the candidate login form. The company side gets these from Identity; this side
// hashes its own passwords, so it owns the arithmetic — and the arithmetic is where the interesting
// mistakes are, which is why it is a domain method taking its clock rather than reading one.
public class CandidateLockoutTests
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan Duration = TimeSpan.FromMinutes(15);
    private static readonly DateTime Now = new(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);

    private static CandidateAccount NewAccount() =>
        CandidateAccount.Register("ada@acme.test", "hash", "Ada", "Applicant", "en");

    [Fact]
    public void A_fresh_account_is_not_locked()
    {
        Assert.False(NewAccount().IsLockedOut(Now));
    }

    [Fact]
    public void Failures_below_the_limit_do_not_lock()
    {
        var account = NewAccount();

        for (var attempt = 0; attempt < MaxAttempts - 1; attempt += 1)
            account.RegisterFailedLogin(MaxAttempts, Duration, Now);

        Assert.False(account.IsLockedOut(Now));
        Assert.Equal(MaxAttempts - 1, account.FailedLoginCount);
    }

    [Fact]
    public void The_limit_attempt_locks_the_account()
    {
        var account = NewAccount();

        for (var attempt = 0; attempt < MaxAttempts; attempt += 1)
            account.RegisterFailedLogin(MaxAttempts, Duration, Now);

        Assert.True(account.IsLockedOut(Now));
        Assert.Equal(Now + Duration, account.LockoutEndsAtUtc);
    }

    [Fact]
    public void The_lockout_expires_on_its_own()
    {
        // The reason it is a timestamp and not a flag. A lockout that never lifts hands anyone who
        // knows a candidate's email a permanent denial of service against them.
        var account = NewAccount();
        for (var attempt = 0; attempt < MaxAttempts; attempt += 1)
            account.RegisterFailedLogin(MaxAttempts, Duration, Now);

        Assert.True(account.IsLockedOut(Now + Duration - TimeSpan.FromSeconds(1)));
        Assert.False(account.IsLockedOut(Now + Duration));
        Assert.False(account.IsLockedOut(Now + Duration + TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Locking_resets_the_counter_so_the_lock_cannot_become_permanent()
    {
        // Without this the count stays at the limit, and every single failure afterwards re-locks the
        // account — a temporary lockout that renews for as long as anyone keeps guessing, which is
        // exactly the permanent lock the expiry was designed to avoid.
        var account = NewAccount();
        for (var attempt = 0; attempt < MaxAttempts; attempt += 1)
            account.RegisterFailedLogin(MaxAttempts, Duration, Now);

        Assert.Equal(0, account.FailedLoginCount);

        var afterExpiry = Now + Duration;
        account.RegisterFailedLogin(MaxAttempts, Duration, afterExpiry);

        Assert.False(account.IsLockedOut(afterExpiry));
    }

    [Fact]
    public void Clearing_ends_a_lockout_and_zeroes_the_count()
    {
        var account = NewAccount();
        for (var attempt = 0; attempt < MaxAttempts; attempt += 1)
            account.RegisterFailedLogin(MaxAttempts, Duration, Now);

        account.ClearLockout();

        Assert.False(account.IsLockedOut(Now));
        Assert.Equal(0, account.FailedLoginCount);
        Assert.Null(account.LockoutEndsAtUtc);
    }

    [Fact]
    public void Setting_a_new_password_ends_the_lockout()
    {
        // Login answers a locked account exactly like a wrong password, so a locked-out candidate
        // cannot tell why they are refused. The reset link is the way out, and it only works if
        // changing the password clears the lock.
        var account = NewAccount();
        for (var attempt = 0; attempt < MaxAttempts; attempt += 1)
            account.RegisterFailedLogin(MaxAttempts, Duration, Now);

        account.ChangePassword("new-hash");

        Assert.False(account.IsLockedOut(Now));
        Assert.Equal(0, account.FailedLoginCount);
    }
}
