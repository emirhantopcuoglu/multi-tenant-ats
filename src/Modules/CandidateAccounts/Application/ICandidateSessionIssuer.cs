using Ats.Modules.CandidateAccounts.Domain;

namespace Ats.Modules.CandidateAccounts.Application;

// The single owner of "what a candidate session is": mint an access token, mint a refresh token, store
// the refresh half hashed and stamped. Two callers need exactly this today — CandidateAuthService
// (register/login/refresh) and CandidateProfileService (a password change rotates the stamp, so the
// caller's own session has to be re-issued) — and the two must not drift. A ProfileService that minted
// an access token but forgot to store a refresh row would hand the candidate a session that dies
// silently in fifteen minutes, which is the exact bug this whole change is fixing.
//
// It also keeps the token hash in one place: hashing on the way in and hashing to look up have to
// agree, and that is not a rule worth restating in a second file.
public interface ICandidateSessionIssuer
{
    /// <summary>
    /// Mints an access/refresh pair for the account and persists the refresh half. Shares the calling
    /// scope's DbContext, so a revocation staged by the caller commits in the same transaction.
    /// </summary>
    Task<CandidateAuthResult> IssueAsync(CandidateAccount account);

    /// <summary>
    /// Finds the stored row for a presented refresh token, or null when no row matches. Says nothing
    /// about whether the row is still redeemable — that is <see cref="CandidateRefreshToken"/>'s call.
    /// </summary>
    Task<CandidateRefreshToken?> FindAsync(string refreshToken);
}
