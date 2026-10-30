using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

public static class CandidateProfileErrors
{
    public static readonly Error NotFound =
        new("candidate_profile.not_found", "Candidate account not found.");

    public static readonly Error UnsupportedLocation =
        new("candidate_profile.unsupported_location",
            "Country and city must be chosen from the supported list.");

    public static readonly Error UnsupportedLanguage =
        new("candidate_profile.unsupported_language",
            "Language must be one of the supported languages.");

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

    // One error for unknown, expired and used tokens, so guesses reveal nothing.
    public static readonly Error InvalidEmailChangeToken =
        new("candidate_profile.invalid_email_change_token",
            "This email change link is invalid, expired or already used.");

    public static readonly Error CvNotFound =
        new("candidate_profile.cv_not_found", "No CV is attached to this account.");

    // A domain rule violation, returned as a 400 with the rule's message.
    public static Error InvalidData(string message) =>
        new("candidate_profile.invalid_data", message);
}
