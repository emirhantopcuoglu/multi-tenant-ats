using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

public static class CandidateProfileErrors
{
    public static readonly Error NotFound =
        new("candidate_profile.not_found", "Candidate account not found.");

    public static readonly Error UnsupportedLocation =
        new("candidate_profile.unsupported_location",
            "Country and city must be chosen from the supported list.");

    // Deliberately does not distinguish "wrong password" from anything subtler: the caller is already
    // authenticated, so unlike login there is nothing to hide — but there is also nothing more to say.
    public static readonly Error InvalidCurrentPassword =
        new("candidate_profile.invalid_current_password", "The current password is incorrect.");

    public static readonly Error PasswordTooShort =
        new("candidate_profile.password_too_short",
            $"Password must be at least {CandidatePasswordPolicy.MinimumLength} characters.");

    public static readonly Error EmailAlreadyRegistered =
        new("candidate_profile.email_already_registered", "An account with this email already exists.");

    public static readonly Error EmailUnchanged =
        new("candidate_profile.email_unchanged", "The new email is the same as the current one.");

    public static readonly Error InvalidEmail =
        new("candidate_profile.invalid_email", "The new email address is not valid.");

    // One error for "unknown", "expired" and "already used" alike: the caller only holds a token, so
    // distinguishing the cases would tell an attacker which guesses were once real.
    public static readonly Error InvalidEmailChangeToken =
        new("candidate_profile.invalid_email_change_token",
            "This email change link is invalid, expired or already used.");

    // Wraps a domain invariant violation (phone format, birth date range, half-filled location) so
    // the API can answer 400 with the exact rule that failed instead of a generic 500.
    public static Error InvalidData(string message) =>
        new("candidate_profile.invalid_data", message);
}
