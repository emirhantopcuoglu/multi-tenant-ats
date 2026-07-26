using Ats.Modules.CandidateAccounts.Domain;

namespace Ats.UnitTests.CandidateAccounts;

public class PasswordResetRequestTests
{
    private const string TokenHash = "hashed-token";

    [Fact]
    public void A_fresh_request_is_valid()
    {
        var request = PasswordResetRequest.Create(Guid.NewGuid(), TokenHash);

        Assert.True(request.IsValid);
        Assert.Null(request.ConsumedAtUtc);
    }

    [Fact]
    public void Create_should_reject_a_missing_account_id()
    {
        Assert.Throws<ArgumentException>(() => PasswordResetRequest.Create(Guid.Empty, TokenHash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_should_reject_a_missing_token_hash(string tokenHash)
    {
        Assert.Throws<ArgumentException>(() => PasswordResetRequest.Create(Guid.NewGuid(), tokenHash));
    }

    [Fact]
    public void A_consumed_request_is_no_longer_valid()
    {
        // Single use is the whole guarantee: a mailed link that kept working would let anyone who
        // ever saw the email reset the password again later.
        var request = PasswordResetRequest.Create(Guid.NewGuid(), TokenHash);

        request.MarkConsumed();

        Assert.False(request.IsValid);
    }

    [Fact]
    public void Consuming_twice_should_throw()
    {
        var request = PasswordResetRequest.Create(Guid.NewGuid(), TokenHash);
        request.MarkConsumed();

        Assert.Throws<InvalidOperationException>(() => request.MarkConsumed());
    }

    [Fact]
    public void The_validity_window_is_an_hour_from_creation()
    {
        // Bounded on purpose: for as long as the link lives, whoever can read that mailbox can take
        // the account. The exact number is asserted so widening it has to be a deliberate edit.
        var request = PasswordResetRequest.Create(Guid.NewGuid(), TokenHash);

        Assert.Equal(60, PasswordResetRequest.ValidMinutes);
        Assert.Equal(request.CreatedAtUtc.AddMinutes(60), request.ExpiresAtUtc);
    }
}
