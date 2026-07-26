namespace Ats.Modules.CandidateAccounts.Domain;

// A candidate's refresh token, stored as a hash so a database leak cannot be replayed as a session.
// Mirrors the company Tenants.RefreshToken with one addition that matters: it records the account's
// SecurityStamp as it stood when the token was issued.
//
// Why the stamp is here. A candidate access token carries the stamp and CandidateSecurityStampHandler
// compares it on every request, which is what makes a password change revoke live sessions instantly.
// A refresh token that ignored the stamp would quietly undo that: the thief of a refresh token could
// mint a brand-new access token carrying the *current* stamp and keep access straight through the
// password change the owner made to lock them out. Binding the stamp here closes that, and does so
// automatically — ChangePassword, ChangeEmail and Delete all rotate the stamp, so all three
// invalidate outstanding refresh tokens without any of them having to know this type exists.
public sealed class CandidateRefreshToken
{
    public Guid Id { get; private set; }
    public Guid CandidateAccountId { get; private set; }
    public string TokenHash { get; private set; }

    // The account's stamp at issue time. Compared on redemption; a mismatch means the account's
    // credentials or identity changed since, so the token is spent even if it has not expired.
    public Guid SecurityStamp { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    private CandidateRefreshToken(
        Guid id, Guid candidateAccountId, string tokenHash, Guid securityStamp, DateTime expiresAtUtc)
    {
        Id = id;
        CandidateAccountId = candidateAccountId;
        TokenHash = tokenHash;
        SecurityStamp = securityStamp;
        ExpiresAtUtc = expiresAtUtc;
    }

    private CandidateRefreshToken() { TokenHash = null!; }

    public static CandidateRefreshToken Issue(
        Guid candidateAccountId, string tokenHash, Guid securityStamp, DateTime expiresAtUtc)
        => new(Guid.NewGuid(), candidateAccountId, tokenHash, securityStamp, expiresAtUtc);

    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;

    // Redeemable only while unrevoked, unexpired, AND still matching the account's stamp. The caller
    // passes the account's current stamp rather than this type reading it, so the rule stays a pure
    // function and the reason a redemption failed is decided in one place.
    public bool CanBeRedeemedWith(Guid currentSecurityStamp) =>
        IsActive && SecurityStamp == currentSecurityStamp;

    public void Revoke() => RevokedAtUtc = DateTime.UtcNow;
}
