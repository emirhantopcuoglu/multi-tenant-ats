namespace Ats.Modules.Tenants.Domain;

public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    private RefreshToken(Guid id, Guid userId, string tokenHash, DateTime expiresAtUtc)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    private RefreshToken() { TokenHash = null!; }

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTime expiresAtUtc)
        => new(Guid.NewGuid(), userId, tokenHash, expiresAtUtc);

    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;

    public void Revoke() => RevokedAtUtc = DateTime.UtcNow;
}