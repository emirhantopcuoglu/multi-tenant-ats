using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

// For candidates who can't sign in. Changing a known password lives in ICandidateProfileService.
public interface ICandidatePasswordResetService
{
    /// <summary>
    /// Mails a reset link if the account exists. Always succeeds, so it can't reveal registered emails.
    /// </summary>
    Task<Result> RequestAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Sets a new password from a mailed token and ends every existing session. Returns no tokens.
    /// </summary>
    Task<Result> ResetAsync(string token, string newPassword, CancellationToken ct = default);
}
