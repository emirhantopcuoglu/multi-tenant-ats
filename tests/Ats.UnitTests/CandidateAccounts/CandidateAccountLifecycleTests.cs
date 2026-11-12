using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;

namespace Ats.UnitTests.CandidateAccounts;

public sealed class CandidateAccountLifecycleTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 15, 0, 0, DateTimeKind.Utc);

    private static CandidateAccount CreateAccount() =>
        CandidateAccount.Register("jane@example.com", "hashed-password", "Jane", "Doe", SupportedLanguages.Default);

    private static CandidateAccount CreateAccountWithProfile()
    {
        var account = CreateAccount();
        account.UpdateProfile(
            "Jane", "Doe", "+905321234567", "Türkiye", "İstanbul", new DateOnly(1995, 5, 1));
        return account;
    }

    [Fact]
    public void Register_should_create_an_active_account()
    {
        var account = CreateAccount();

        Assert.Equal(CandidateAccountStatus.Active, account.Status);
        Assert.True(account.IsActive);
        Assert.Null(account.FrozenAtUtc);
        Assert.Null(account.DeletionDueAtUtc);
        Assert.Null(account.DeletedAtUtc);
    }

    [Fact]
    public void Freeze_should_mark_the_account_frozen_without_rotating_the_stamp()
    {
        // Arrange
        var account = CreateAccount();
        var stampBefore = account.SecurityStamp;

        // Act
        account.Freeze();

        // Assert: a frozen account keeps its session (it must reach the reactivation screen)
        Assert.Equal(CandidateAccountStatus.Frozen, account.Status);
        Assert.False(account.IsActive);
        Assert.NotNull(account.FrozenAtUtc);
        Assert.Equal(stampBefore, account.SecurityStamp);
    }

    [Fact]
    public void Freeze_should_reject_a_non_active_account()
    {
        var account = CreateAccount();
        account.Freeze();

        Assert.Throws<InvalidOperationException>(account.Freeze);
    }

    [Fact]
    public void Reactivate_should_restore_a_frozen_account()
    {
        // Arrange
        var account = CreateAccount();
        account.Freeze();

        // Act
        account.Reactivate();

        // Assert
        Assert.Equal(CandidateAccountStatus.Active, account.Status);
        Assert.True(account.IsActive);
        Assert.Null(account.FrozenAtUtc);
    }

    [Fact]
    public void Reactivate_should_reject_an_account_that_is_not_frozen()
    {
        Assert.Throws<InvalidOperationException>(CreateAccount().Reactivate);
    }

    [Fact]
    public void RequestDeletion_should_schedule_the_erasure_two_weeks_out_and_keep_the_data()
    {
        var account = CreateAccountWithProfile();

        account.RequestDeletion(Now);

        Assert.Equal(CandidateAccountStatus.PendingDeletion, account.Status);
        Assert.False(account.IsActive);
        Assert.Equal(Now.AddDays(14), account.DeletionDueAtUtc);
        Assert.Null(account.DeletedAtUtc);
        Assert.Equal("Jane", account.FirstName);
        Assert.Equal("+905321234567", account.PhoneNumber);
    }

    // Postgres would round the ticks away, and the date read back later must match the one shown now.
    [Fact]
    public void RequestDeletion_should_keep_the_due_date_to_whole_seconds()
    {
        var account = CreateAccount();

        account.RequestDeletion(Now.AddTicks(6_583_499));

        Assert.Equal(Now.AddDays(14), account.DeletionDueAtUtc);
    }

    // The request may come from a stolen session; getting back in to cancel needs the password.
    [Fact]
    public void RequestDeletion_should_end_every_session()
    {
        var account = CreateAccount();
        var stampBefore = account.SecurityStamp;

        account.RequestDeletion(Now);

        Assert.NotEqual(stampBefore, account.SecurityStamp);
    }

    [Fact]
    public void RequestDeletion_should_work_on_a_frozen_account_and_clear_the_frozen_timestamp()
    {
        var account = CreateAccount();
        account.Freeze();

        account.RequestDeletion(Now);

        Assert.Equal(CandidateAccountStatus.PendingDeletion, account.Status);
        Assert.Null(account.FrozenAtUtc);
    }

    [Fact]
    public void RequestDeletion_should_reject_an_account_already_waiting_for_deletion()
    {
        var account = CreateAccount();
        account.RequestDeletion(Now);

        Assert.Throws<InvalidOperationException>(() => account.RequestDeletion(Now.AddDays(1)));
        Assert.Equal(Now.AddDays(14), account.DeletionDueAtUtc);
    }

    [Fact]
    public void CancelDeletion_should_restore_the_account_as_it_was()
    {
        var account = CreateAccountWithProfile();
        account.RequestDeletion(Now);

        account.CancelDeletion(Now.AddDays(14).AddSeconds(-1));

        Assert.Equal(CandidateAccountStatus.Active, account.Status);
        Assert.Null(account.DeletionDueAtUtc);
        Assert.Equal("Jane", account.FirstName);
    }

    [Fact]
    public void CancelDeletion_should_reject_an_account_without_a_deletion_request()
    {
        Assert.Throws<InvalidOperationException>(() => CreateAccount().CancelDeletion(Now));
    }

    // The erasure may already have reached other modules; cancelling then would leave a half-erased account.
    [Fact]
    public void CancelDeletion_should_refuse_once_the_grace_period_has_ended()
    {
        var account = CreateAccount();
        account.RequestDeletion(Now);

        Assert.Throws<InvalidOperationException>(() => account.CancelDeletion(Now.AddDays(14)));
        Assert.Equal(CandidateAccountStatus.PendingDeletion, account.Status);
    }

    [Fact]
    public void Erase_should_anonymize_every_personal_field()
    {
        var account = CreateAccountWithProfile();
        account.RequestDeletion(Now);

        account.Erase(Now.AddDays(14));

        // Right to erasure: nothing personally identifying may survive on the row.
        Assert.Equal(CandidateAccountStatus.Deleted, account.Status);
        Assert.False(account.IsActive);
        Assert.NotNull(account.DeletedAtUtc);
        Assert.Null(account.DeletionDueAtUtc);
        Assert.Equal(AnonymizedEmail.For(account.Id), account.Email);
        Assert.Equal(CandidateAccount.AnonymizedFirstName, account.FirstName);
        Assert.Equal(CandidateAccount.AnonymizedLastName, account.LastName);
        Assert.Null(account.PhoneNumber);
        Assert.Null(account.Country);
        Assert.Null(account.City);
        Assert.Null(account.BirthDate);
        Assert.Null(account.CvFileKey);
    }

    // A candidate may sign in during the grace period without cancelling.
    [Fact]
    public void Erase_should_end_sessions_opened_during_the_grace_period()
    {
        var account = CreateAccount();
        account.RequestDeletion(Now);
        var stampDuringGrace = account.SecurityStamp;

        account.Erase(Now.AddDays(14));

        Assert.NotEqual(stampDuringGrace, account.SecurityStamp);
    }

    [Fact]
    public void Erase_should_refuse_before_the_grace_period_ends()
    {
        var account = CreateAccount();
        account.RequestDeletion(Now);

        Assert.Throws<InvalidOperationException>(() => account.Erase(Now.AddDays(14).AddSeconds(-1)));
        Assert.Equal(CandidateAccountStatus.PendingDeletion, account.Status);
    }

    [Fact]
    public void Erase_should_refuse_an_account_whose_deletion_was_cancelled()
    {
        var account = CreateAccount();
        account.RequestDeletion(Now);
        account.CancelDeletion(Now);

        Assert.Throws<InvalidOperationException>(() => account.Erase(Now.AddDays(30)));
        Assert.Equal(CandidateAccountStatus.Active, account.Status);
    }

    [Fact]
    public void Erase_should_reject_an_already_erased_account()
    {
        var account = CreateAccount();
        account.RequestDeletion(Now);
        account.Erase(Now.AddDays(14));

        Assert.Throws<InvalidOperationException>(() => account.Erase(Now.AddDays(15)));
    }
}
