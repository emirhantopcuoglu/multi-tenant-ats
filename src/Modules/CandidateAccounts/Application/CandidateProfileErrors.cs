using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

public static class CandidateProfileErrors
{
    public static readonly Error NotFound =
        new("candidate_profile.not_found", "Candidate account not found.");

    public static readonly Error UnsupportedLocation =
        new("candidate_profile.unsupported_location",
            "Country and city must be chosen from the supported list.");

    // Wraps a domain invariant violation (phone format, birth date range, half-filled location) so
    // the API can answer 400 with the exact rule that failed instead of a generic 500.
    public static Error InvalidData(string message) =>
        new("candidate_profile.invalid_data", message);
}
