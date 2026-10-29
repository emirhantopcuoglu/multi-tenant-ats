namespace Ats.Modules.CandidateAccounts.Domain;

// Only the SHA-256 hash of the token is stored. The raw token is in the link sent to the new address.
public sealed class EmailChangeRequest
{
    public Guid Id { get; private set; }
    public Guid CandidateAccountId { get; private set; }

    public string NewEmail { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? ConsumedAtUtc { get; private set; }

    public const int ValidMinutes = 60;

    private EmailChangeRequest() { }

    private EmailChangeRequest(Guid id, Guid candidateAccountId, string newEmail, string tokenHash)
    {
        Id = id;
        CandidateAccountId = candidateAccountId;
        NewEmail = newEmail;
        TokenHash = tokenHash;
        CreatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = CreatedAtUtc.AddMinutes(ValidMinutes);
    }

    public static EmailChangeRequest Create(Guid candidateAccountId, string newEmail, string tokenHash)
    {
        if (candidateAccountId == Guid.Empty)
            throw new ArgumentException("Candidate account id is required.", nameof(candidateAccountId));
        if (string.IsNullOrWhiteSpace(newEmail))
            throw new ArgumentException("New email is required.", nameof(newEmail));
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));

        return new EmailChangeRequest(
            Guid.NewGuid(), candidateAccountId, CandidateAccount.NormalizeEmail(newEmail), tokenHash);
    }

    public bool IsValid => ConsumedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;

    public void MarkConsumed()
    {
        if (ConsumedAtUtc is not null)
            throw new InvalidOperationException("This email change request was already consumed.");

        ConsumedAtUtc = DateTime.UtcNow;
    }
}
