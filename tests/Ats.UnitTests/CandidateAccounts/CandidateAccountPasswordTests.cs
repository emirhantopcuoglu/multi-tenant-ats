using Ats.Modules.CandidateAccounts.Domain;

namespace Ats.UnitTests.CandidateAccounts;

public class CandidateAccountPasswordTests
{
    private const string PasswordHash = "hashed-password";
    private const string NewPasswordHash = "new-hashed-password";

    private static CandidateAccount CreateAccount() =>
        CandidateAccount.Register("jane@example.com", PasswordHash, "Jane", "Doe");

    [Fact]
    public void Register_should_issue_a_security_stamp()
    {
        var account = CreateAccount();

        Assert.NotEqual(Guid.Empty, account.SecurityStamp);
    }

    [Fact]
    public void ChangePassword_should_replace_the_password_hash()
    {
        var account = CreateAccount();

        account.ChangePassword(NewPasswordHash);

        Assert.Equal(NewPasswordHash, account.PasswordHash);
    }

    [Fact]
    public void ChangePassword_should_rotate_the_security_stamp()
    {
        // The stamp is what ties issued tokens to the account's security state: rotating it is the
        // whole mechanism by which a password change kills every previously issued token.
        var account = CreateAccount();
        var stampBeforeChange = account.SecurityStamp;

        account.ChangePassword(NewPasswordHash);

        Assert.NotEqual(stampBeforeChange, account.SecurityStamp);
        Assert.NotEqual(Guid.Empty, account.SecurityStamp);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ChangePassword_should_reject_a_missing_password_hash(string? newPasswordHash)
    {
        var account = CreateAccount();

        Assert.Throws<ArgumentException>(() => account.ChangePassword(newPasswordHash!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ChangePassword_should_keep_the_current_state_when_rejecting(string? newPasswordHash)
    {
        // A failed guard must leave the aggregate untouched — otherwise a rejected change could
        // still log every session out by rotating the stamp.
        var account = CreateAccount();
        var stampBeforeChange = account.SecurityStamp;

        Assert.Throws<ArgumentException>(() => account.ChangePassword(newPasswordHash!));

        Assert.Equal(PasswordHash, account.PasswordHash);
        Assert.Equal(stampBeforeChange, account.SecurityStamp);
    }
}
