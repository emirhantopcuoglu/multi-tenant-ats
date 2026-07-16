using Ats.Modules.CandidateAccounts.Domain;

namespace Ats.UnitTests.CandidateAccounts;

public sealed class CandidateAccountEmailChangeTests
{
    private static CandidateAccount CreateAccount() =>
        CandidateAccount.Register("jane@example.com", "hashed-password", "Jane", "Doe");

    [Fact]
    public void ChangeEmail_should_normalize_and_rotate_the_security_stamp()
    {
        // Arrange
        var account = CreateAccount();
        var stampBefore = account.SecurityStamp;

        // Act
        account.ChangeEmail("  New@Example.COM ");

        // Assert — email is the login identity, so every issued token must die with the change
        Assert.Equal("new@example.com", account.Email);
        Assert.NotEqual(stampBefore, account.SecurityStamp);
    }

    [Fact]
    public void ChangeEmail_should_reject_a_blank_email_and_change_nothing()
    {
        // Arrange
        var account = CreateAccount();
        var stampBefore = account.SecurityStamp;

        // Act + Assert
        Assert.Throws<ArgumentException>(() => account.ChangeEmail("  "));
        Assert.Equal("jane@example.com", account.Email);
        Assert.Equal(stampBefore, account.SecurityStamp);
    }

    [Fact]
    public void Create_should_normalize_the_email_and_expire_in_one_hour()
    {
        // Act
        var request = EmailChangeRequest.Create(Guid.NewGuid(), " New@Example.COM ", "token-hash");

        // Assert
        Assert.Equal("new@example.com", request.NewEmail);
        Assert.True(request.IsValid);
        var expectedExpiry = request.CreatedAtUtc.AddMinutes(EmailChangeRequest.ValidMinutes);
        Assert.Equal(expectedExpiry, request.ExpiresAtUtc);
    }

    [Fact]
    public void MarkConsumed_should_invalidate_the_request_exactly_once()
    {
        // Arrange
        var request = EmailChangeRequest.Create(Guid.NewGuid(), "new@example.com", "token-hash");

        // Act
        request.MarkConsumed();

        // Assert — a confirmation link is single-use; a second consume is a programming error
        Assert.False(request.IsValid);
        Assert.Throws<InvalidOperationException>(request.MarkConsumed);
    }

    [Theory]
    [InlineData("", "token-hash")]
    [InlineData("new@example.com", " ")]
    public void Create_should_reject_missing_inputs(string newEmail, string tokenHash)
    {
        Assert.Throws<ArgumentException>(() => EmailChangeRequest.Create(Guid.NewGuid(), newEmail, tokenHash));
    }
}
