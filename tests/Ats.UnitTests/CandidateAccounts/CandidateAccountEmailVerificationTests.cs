using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;

namespace Ats.UnitTests.CandidateAccounts;

public class CandidateAccountEmailVerificationTests
{
    [Fact]
    public void a_new_account_should_start_unverified()
    {
        var account = Register();

        Assert.False(account.IsEmailVerified);
        Assert.Null(account.EmailVerifiedAtUtc);
    }

    [Fact]
    public void MarkEmailVerified_should_report_that_it_verified_the_account()
    {
        var account = Register();

        Assert.True(account.MarkEmailVerified());
        Assert.True(account.IsEmailVerified);
    }

    [Fact]
    public void MarkEmailVerified_should_keep_the_first_timestamp_and_report_no_change()
    {
        // A double-clicked link must not move the record of when the address was actually proven.
        var account = Register();
        account.MarkEmailVerified();
        var firstVerification = account.EmailVerifiedAtUtc;

        Assert.False(account.MarkEmailVerified());
        Assert.Equal(firstVerification, account.EmailVerifiedAtUtc);
    }

    [Fact]
    public void changing_the_email_should_verify_the_new_address()
    {
        // ChangeEmail only runs after the new address's link was clicked, so the address is already
        // verified. This is also how a mistyped registration gets fixed.
        var account = Register();

        account.ChangeEmail("corrected@acme.test");

        Assert.True(account.IsEmailVerified);
        Assert.Equal("corrected@acme.test", account.Email);
    }

    [Fact]
    public void changing_the_email_should_re_verify_even_an_already_verified_account()
    {
        // The timestamp must track the address currently on the account, not the first one ever
        // proven, otherwise "verified on the 3rd" would refer to a mailbox no longer in use.
        var account = Register();
        account.MarkEmailVerified();
        var firstVerification = account.EmailVerifiedAtUtc;

        account.ChangeEmail("moved@acme.test");

        Assert.True(account.IsEmailVerified);
        Assert.True(account.EmailVerifiedAtUtc >= firstVerification);
    }

    private static CandidateAccount Register() =>
        CandidateAccount.Register("typo@acme.test", "hash", "Test", "Candidate", SupportedLanguages.Default);
}
