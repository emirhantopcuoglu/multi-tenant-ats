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
}
