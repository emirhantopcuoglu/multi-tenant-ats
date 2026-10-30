namespace Ats.Modules.CandidateAccounts.Domain;

public enum CandidateAccountStatus
{
    Active,

    // Can still sign in and reactivate, but can't apply.
    Frozen,

    // Anonymized and hidden by the query filter. The row stays for application references.
    Deleted
}
