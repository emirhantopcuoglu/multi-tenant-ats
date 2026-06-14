using Ats.Shared.Kernel;

namespace Ats.Modules.Tenants.Domain;

public sealed class Invitation : ITenantScoped
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = null!;
    public string Role { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }

    private Invitation(Guid id, string email, string role, string tokenHash, DateTime expiresAtUtc)
    {
        Id = id;
        Email = email;
        Role = role;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    private Invitation() { }

    public static Invitation Create(string email, string role, string tokenHash, int validDays)
        => new(Guid.NewGuid(), email.ToLowerInvariant(), role, tokenHash, DateTime.UtcNow.AddDays(validDays));

    public bool IsValid => AcceptedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;

    public void MarkAccepted() => AcceptedAtUtc = DateTime.UtcNow;
}
