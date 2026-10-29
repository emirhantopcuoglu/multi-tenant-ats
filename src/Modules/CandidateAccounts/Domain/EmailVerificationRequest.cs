namespace Ats.Modules.CandidateAccounts.Domain;

// Only the SHA-256 hash of the token is stored.
public sealed class EmailVerificationRequest
{
    public Guid Id { get; private set; }
    public Guid CandidateAccountId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? ConsumedAtUtc { get; private set; }

    // Longer than reset/email-change links: this one can't be used to take over an account.
    public const int ValidHours = 24;

    private EmailVerificationRequest() { }

    private EmailVerificationRequest(Guid id, Guid candidateAccountId, string tokenHash)
    {
        Id = id;
        CandidateAccountId = candidateAccountId;
        TokenHash = tokenHash;
        CreatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = CreatedAtUtc.AddHours(ValidHours);
    }

    public static EmailVerificationRequest Create(Guid candidateAccountId, string tokenHash)
    {
        if (candidateAccountId == Guid.Empty)
            throw new ArgumentException("Candidate account id is required.", nameof(candidateAccountId));
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));

        return new EmailVerificationRequest(Guid.NewGuid(), candidateAccountId, tokenHash);
    }

    public bool IsValid => ConsumedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;

    public void MarkConsumed()
    {
        if (ConsumedAtUtc is not null)
            throw new InvalidOperationException("This email verification request was already consumed.");

        ConsumedAtUtc = DateTime.UtcNow;
    }
}
