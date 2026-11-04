namespace Ats.Modules.Tenants.Domain;

// A login starts a family and each refresh continues it, so a replayed token can end the whole chain.
public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    private RefreshToken(Guid id, Guid userId, Guid familyId, string tokenHash, DateTime expiresAtUtc)
    {
        Id = id;
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    private RefreshToken() { TokenHash = null!; }

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTime expiresAtUtc)
    {
        var id = Guid.NewGuid();
        return new(id, userId, id, tokenHash, expiresAtUtc);
    }

    public bool IsRevoked => RevokedAtUtc is not null;

    public bool IsActive => !IsRevoked && ExpiresAtUtc > DateTime.UtcNow;

    public void Revoke() => RevokedAtUtc = DateTime.UtcNow;

    public RefreshToken Rotate(string tokenHash, DateTime expiresAtUtc)
    {
        Revoke();
        return new(Guid.NewGuid(), UserId, FamilyId, tokenHash, expiresAtUtc);
    }
}
