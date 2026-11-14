using Ats.Modules.CandidateAccounts.Domain;

namespace Ats.Modules.CandidateAccounts.Application;

// Used by both the auth and profile services so session creation can't drift between them.
public interface ICandidateSessionIssuer
{
    /// <summary>
    /// Starts a new session: creates an access/refresh pair and saves the refresh token.
    /// </summary>
    Task<CandidateAuthResult> IssueAsync(CandidateAccount account);

    /// <summary>
    /// Revokes a refresh token and issues its successor in the same session, in one save. Throws
    /// DbUpdateConcurrencyException if a parallel request rotated the token first.
    /// </summary>
    Task<CandidateAuthResult> RotateAsync(CandidateRefreshToken spent, CandidateAccount account);

    /// <summary>
    /// Revokes every still-active token in a session and returns how many there were.
    /// </summary>
    Task<int> RevokeFamilyAsync(Guid familyId);

    /// <summary>
    /// Finds the stored row for a refresh token, or null. Does not check whether it is still valid.
    /// </summary>
    Task<CandidateRefreshToken?> FindAsync(string refreshToken);
}
