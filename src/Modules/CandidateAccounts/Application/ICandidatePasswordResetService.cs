using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

// The unauthenticated half of password management, kept apart from ICandidateProfileService: that one
// serves a signed-in candidate who re-proves ownership with their current password, while this one is
// for someone who cannot sign in at all and proves ownership by reading their mailbox. The two have
// opposite trust assumptions, so they do not belong on the same interface.
public interface ICandidatePasswordResetService
{
    /// <summary>
    /// Mails a reset link if an account with this email exists. Reports success either way — see
    /// <see cref="CandidatePasswordResetErrors"/> — so the endpoint cannot be used to discover which
    /// addresses are registered.
    /// </summary>
    Task<Result> RequestAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Sets a new password from a mailed token, consuming it. Rotates the account's security stamp,
    /// which revokes every existing session — including the refresh tokens issued under the old stamp.
    /// Deliberately returns no tokens: whoever reset the password signs in with it.
    /// </summary>
    Task<Result> ResetAsync(string token, string newPassword, CancellationToken ct = default);
}
