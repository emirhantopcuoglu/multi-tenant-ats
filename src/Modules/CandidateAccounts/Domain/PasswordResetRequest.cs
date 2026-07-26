namespace Ats.Modules.CandidateAccounts.Domain;

// A pending "I forgot my password" intent, following the same shape as EmailChangeRequest: the row
// stores only the SHA-256 HASH of the token, never the token itself. The raw token exists in exactly
// one place — the link mailed to the account's address — so a leaked database dump cannot be replayed
// to take over accounts.
//
// This token is strictly more powerful than the email-change one: presenting it sets a new password
// with no other proof of ownership, so possession of the mailbox is the whole authentication. That is
// why it is short-lived, single-use, and superseded by any newer request for the same account.
public sealed class PasswordResetRequest
{
    public Guid Id { get; private set; }
    public Guid CandidateAccountId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    // Set exactly once — a reset link is single-use. A consumed request stays in the table as an
    // audit trail of when the password was reset and from which request.
    public DateTime? ConsumedAtUtc { get; private set; }

    // Same one-hour window as EmailChangeRequest, for the same reason: it only needs to cover "open
    // inbox, click link". Bounding it matters more here — for as long as the link is live, whoever
    // can read that mailbox can take the account.
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
