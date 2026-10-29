using Ats.Modules.CandidateAccounts.Domain;

namespace Ats.Modules.CandidateAccounts.Application;

// Used by both the auth and profile services so session creation can't drift between them.
public interface ICandidateSessionIssuer
{
    /// Creates an access/refresh pair and saves the refresh token. Uses the caller's DbContext, so a
    /// revocation the caller staged is saved in the same transaction.
    Task<CandidateAuthResult> IssueAsync(CandidateAccount account);

    /// Finds the stored row for a refresh token, or null. Does not check whether it is still valid.
    Task<CandidateRefreshToken?> FindAsync(string refreshToken);
}
