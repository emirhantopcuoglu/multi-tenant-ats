namespace Ats.Modules.CandidateAccounts.Domain;

// Only the SHA-256 hash of the token is stored. Single-use and short-lived, because the token alone
// is enough to set a new password.
public sealed class PasswordResetRequest
{
    public Guid Id { get; private set; }
    public Guid CandidateAccountId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? ConsumedAtUtc { get; private set; }

    public const int ValidMinutes = 60;

    private PasswordResetRequest() { }

    private PasswordResetRequest(Guid id, Guid candidateAccountId, string tokenHash)
    {
        Id = id;
        CandidateAccountId = candidateAccountId;
        TokenHash = tokenHash;
        CreatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = CreatedAtUtc.AddMinutes(ValidMinutes);
    }

    public static PasswordResetRequest Create(Guid candidateAccountId, string tokenHash)
    {
        if (candidateAccountId == Guid.Empty)
            throw new ArgumentException("Candidate account id is required.", nameof(candidateAccountId));
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));

        return new PasswordResetRequest(Guid.NewGuid(), candidateAccountId, tokenHash);
    }

    public bool IsValid => ConsumedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;

    public void MarkConsumed()
    {
        if (ConsumedAtUtc is not null)
            throw new InvalidOperationException("This password reset request was already consumed.");

        ConsumedAtUtc = DateTime.UtcNow;
    }
}
