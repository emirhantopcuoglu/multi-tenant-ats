using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;

namespace Ats.UnitTests.CandidateAccounts;

public sealed class CandidateAccountLifecycleTests
{
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
        Assert.Null(account.FrozenAtUtc);
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

        // Assert — a frozen account keeps its session (it must reach the reactivation screen)
        Assert.Equal(CandidateAccountStatus.Frozen, account.Status);
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
        Assert.Null(account.FrozenAtUtc);
    }

    [Fact]
    public void Reactivate_should_reject_an_account_that_is_not_frozen()
    {
        Assert.Throws<InvalidOperationException>(CreateAccount().Reactivate);
    }

    [Fact]
    public void Delete_should_anonymize_every_personal_field()
    {
        // Arrange
        var account = CreateAccountWithProfile();

        // Act
        account.Delete();

        // Assert — right to erasure: nothing personally identifying may survive on the row
        Assert.Equal(CandidateAccountStatus.Deleted, account.Status);
        Assert.NotNull(account.DeletedAtUtc);
        Assert.Equal(CandidateAccount.BuildAnonymizedEmail(account.Id), account.Email);
        Assert.Equal(CandidateAccount.AnonymizedFirstName, account.FirstName);
        Assert.Equal(CandidateAccount.AnonymizedLastName, account.LastName);
        Assert.Null(account.PhoneNumber);
        Assert.Null(account.Country);
        Assert.Null(account.City);
        Assert.Null(account.BirthDate);
        Assert.Null(account.CvFileKey);
    }

    [Fact]
    public void Delete_should_rotate_the_security_stamp()
    {
        // Arrange
        var account = CreateAccount();
        var stampBefore = account.SecurityStamp;

        // Act
        account.Delete();

        // Assert — every live session must die the moment the account is deleted
        Assert.NotEqual(stampBefore, account.SecurityStamp);
    }

    [Fact]
    public void Delete_should_work_on_a_frozen_account_and_clear_the_frozen_timestamp()
    {
        var account = CreateAccount();
        account.Freeze();

        account.Delete();

        Assert.Equal(CandidateAccountStatus.Deleted, account.Status);
        Assert.Null(account.FrozenAtUtc);
    }

    [Fact]
    public void Delete_should_reject_an_already_deleted_account()
    {
        var account = CreateAccount();
        account.Delete();

        Assert.Throws<InvalidOperationException>(account.Delete);
    }

    [Fact]
    public void BuildAnonymizedEmail_should_be_unique_per_account_and_undeliverable()
    {
        var first = CandidateAccount.BuildAnonymizedEmail(Guid.NewGuid());
        var second = CandidateAccount.BuildAnonymizedEmail(Guid.NewGuid());

        Assert.NotEqual(first, second);
        // ".invalid" is reserved by RFC 2606 — the placeholder can never receive real mail.
        Assert.EndsWith("@account.invalid", first);
    }
}
