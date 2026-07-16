namespace Ats.Modules.CandidateAccounts.Domain;

// The account's lifecycle state. Stored as its string name (project convention): "Frozen" in a DB
// row or a log line is self-explanatory where a bare 1 is not.
public enum CandidateAccountStatus
{
    Active,

    // Self-service pause, fully reversible: a frozen account can still log in, but the SPA routes
    // it to a reactivation screen instead of the candidate area.
    Frozen,

    // Terminal. The row survives (applications keep a valid account reference) but every personal
    // field is anonymized and a global query filter hides it from all reads.
    Deleted
}
