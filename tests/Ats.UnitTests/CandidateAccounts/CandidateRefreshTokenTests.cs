using Ats.Modules.CandidateAccounts.Domain;

namespace Ats.UnitTests.CandidateAccounts;

public class CandidateRefreshTokenTests
{
    private const string TokenHash = "hashed-token";

    private static CandidateRefreshToken Issue(Guid stamp, int validDays = 7) =>
        CandidateRefreshToken.Issue(
            Guid.NewGuid(), TokenHash, stamp, DateTime.UtcNow.AddDays(validDays));

    [Fact]
    public void A_fresh_token_is_redeemable_with_the_stamp_it_was_issued_for()
    {
        var stamp = Guid.NewGuid();

        Assert.True(Issue(stamp).CanBeRedeemedWith(stamp));
    }

    [Fact]
    public void A_revoked_token_is_not_redeemable()
    {
        var stamp = Guid.NewGuid();
        var token = Issue(stamp);

        token.Revoke();

        Assert.False(token.CanBeRedeemedWith(stamp));
    }

    [Fact]
    public void An_expired_token_is_not_redeemable()
    {
        var stamp = Guid.NewGuid();

        // A negative validity window places the expiry in the past.
        Assert.False(Issue(stamp, validDays: -1).CanBeRedeemedWith(stamp));
    }

    [Fact]
    public void A_token_issued_before_the_stamp_rotated_is_not_redeemable()
    {
        // The case the whole design exists for: the account's password changed (or its email, or it
        // was deleted), rotating the stamp. An unexpired, unrevoked refresh token from before that
        // must not be able to mint a new access token — otherwise changing the password would fail
        // to lock out whoever holds the old refresh token.
        var token = Issue(Guid.NewGuid());

        Assert.False(token.CanBeRedeemedWith(Guid.NewGuid()));
    }

    [Fact]
    public void IsActive_ignores_the_stamp()
    {
        // IsActive answers only "unrevoked and unexpired". Keeping the stamp check out of it is what
        // lets the refresh path tell a stale token apart from a rotated-stamp one when it needs to.
        var token = Issue(Guid.NewGuid());

        Assert.True(token.IsActive);
    }
}
