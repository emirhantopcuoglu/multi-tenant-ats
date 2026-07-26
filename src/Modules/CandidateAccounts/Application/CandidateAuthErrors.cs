using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

public static class CandidateAuthErrors
{
    public static readonly Error EmailAlreadyRegistered =
        new("candidate_auth.email_already_registered", "An account with this email already exists.");

    // One message for "no such email" and "wrong password" alike, so the response never reveals
    // whether an email is registered.
    public static readonly Error InvalidCredentials =
        new("candidate_auth.invalid_credentials", "Invalid email or password.");

    public static readonly Error NotFound =
        new("candidate_auth.not_found", "Candidate account not found.");

    // One message covers unknown, expired, already-rotated and stamp-invalidated tokens alike. The
    // client's only useful response is the same in every case — sign in again — and distinguishing
    // them would tell a token thief which of those situations they are in.
    public static readonly Error InvalidRefreshToken =
        new("candidate_auth.invalid_refresh_token", "The session has expired. Please sign in again.");

    public static readonly Error PasswordTooShort =
        new("candidate_auth.password_too_short",
            $"Password must be at least {CandidatePasswordPolicy.MinimumLength} characters.");
}
