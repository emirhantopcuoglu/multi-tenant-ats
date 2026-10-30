using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

public static class CandidateAuthErrors
{
    public static readonly Error EmailAlreadyRegistered =
        new("candidate_auth.email_already_registered", "An account with this email already exists.");

    // Same error for an unknown email and a wrong password.
    public static readonly Error InvalidCredentials =
        new("candidate_auth.invalid_credentials", "Invalid email or password.");

    public static readonly Error NotFound =
        new("candidate_auth.not_found", "Candidate account not found.");

    // One error for every invalid refresh token; the client signs in again either way.
    public static readonly Error InvalidRefreshToken =
        new("candidate_auth.invalid_refresh_token", "The session has expired. Please sign in again.");

    public static readonly Error PasswordTooShort =
        new("candidate_auth.password_too_short",
            $"Password must be at least {CandidatePasswordPolicy.MinimumLength} characters.");
}
