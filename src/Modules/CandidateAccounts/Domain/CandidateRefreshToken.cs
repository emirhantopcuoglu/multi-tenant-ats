namespace Ats.Modules.CandidateAccounts.Domain;

// Stored hashed. Keeps the account's stamp from issue time, so a stamp rotation (password change,
// email change, delete) also ends refresh tokens, not just access tokens.
public sealed class CandidateRefreshToken
{
    public Guid Id { get; private set; }
    public Guid CandidateAccountId { get; private set; }
    public string TokenHash { get; private set; }

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

    public bool CanBeRedeemedWith(Guid currentSecurityStamp) =>
        IsActive && SecurityStamp == currentSecurityStamp;

    public void Revoke() => RevokedAtUtc = DateTime.UtcNow;
}
