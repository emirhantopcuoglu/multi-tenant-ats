namespace Ats.Shared.Kernel;

// Authorization policy names. These live in the shared kernel so any module's API
// can reference them by name without taking a dependency on another module. The
// mapping of each policy to the roles that satisfy it is wired in the API
// composition root (Program.cs), which is the only place allowed to know both the
// policy names and the concrete role names.
public static class Policies
{
    public const string CanManageJobs = "CanManageJobs";
    public const string CanViewJobs = "CanViewJobs";
    public const string CanManageUsers = "CanManageUsers";
    public const string CanViewApplications = "CanViewApplications";
    public const string CanManageApplications = "CanManageApplications";
    public const string CanViewInterviews = "CanViewInterviews";
    public const string CanManageInterviews = "CanManageInterviews";

    // Resource-based: the current user must appear in the interview's InterviewerUserIds list.
    // Used imperatively via IAuthorizationService, not as an [Authorize] attribute.
    public const string IsInterviewParticipant = "IsInterviewParticipant";

    // The caller must present a candidate (marketplace) token, not a company (tenant-user) token.
    // Both token kinds are signed by the same key and validated by the same JWT scheme, so this
    // policy is what keeps a company token out of candidate-only endpoints — see TokenTypes.
    public const string CandidateOnly = "CandidateOnly";
}

// Discriminates the two kinds of access token the system issues. Both are otherwise identical
// (same signing key, issuer, audience), so a single custom claim tells them apart: a candidate
// (marketplace) token carries token_type=candidate, while a company (tenant-user) token does not.
// Kept in the shared kernel so the token minter and the authorization policy agree on the strings.
public static class TokenTypes
{
    public const string ClaimName = "token_type";
    public const string Candidate = "candidate";
}
