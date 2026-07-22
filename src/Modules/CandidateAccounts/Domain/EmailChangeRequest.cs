namespace Ats.Modules.CandidateAccounts.Domain;

// A pending "change my login email" intent, following the Tenants.Invitation shape: the row stores
// only the SHA-256 HASH of the verification token, never the token itself. The raw token exists in
// exactly one place — the link mailed to the NEW address — so a leaked database dump cannot be
// replayed to take over an account. Clicking that link is what proves the requester controls the
// new mailbox; without proof, a typo'd or hostile address would silently become someone's login.
public sealed class EmailChangeRequest
{
    public Guid Id { get; private set; }
    public Guid CandidateAccountId { get; private set; }

    // Already normalised (CandidateAccount.NormalizeEmail) so the uniqueness re-check at confirm
    // time compares like with like.
    public string NewEmail { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    // Set exactly once — a confirmation link is single-use. A consumed request stays in the table
    // as an audit trail of when the login identity changed.
    public DateTime? ConsumedAtUtc { get; private set; }

    // Short-lived on purpose: the window only needs to cover "open inbox, click link". An hour of
    // validity bounds how long a mistyped or attacker-chosen address stays claimable.
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
