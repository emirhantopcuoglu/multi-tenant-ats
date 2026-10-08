namespace Ats.Modules.CandidateAccounts.Domain;

// A pending "prove you can read this mailbox" intent for the address the account already holds. Same
// shape as PasswordResetRequest and EmailChangeRequest: the row stores only the SHA-256 HASH of the
// token, never the token itself, so a database dump cannot be replayed into a verified account.
//
// Kept as its own entity rather than reusing EmailChangeRequest, despite the near-identical columns.
// That one carries a NewEmail and swaps the login identity when consumed; this one asserts a fact
// about the current address. Merging them would mean a nullable NewEmail whose meaning depends on
// which flow wrote the row — the kind of shared table that makes both flows harder to reason about.
public sealed class EmailVerificationRequest
{
    public Guid Id { get; private set; }
    public Guid CandidateAccountId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    // Set exactly once — a verification link is single-use. A consumed request stays in the table as
    // the audit trail of when the address was proven.
    public DateTime? ConsumedAtUtc { get; private set; }

    // Longer than the one-hour password-reset and email-change windows, on purpose. Those two are
    // account-takeover surfaces where a narrow window is the point. This one only proves a mailbox is
    // readable, and its realistic failure mode is a candidate who registers, closes the tab and comes
    // back that evening — locking them out overnight would create support work for no security gain.
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
