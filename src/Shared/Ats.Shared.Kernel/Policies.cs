namespace Ats.Shared.Kernel;

// Names only, so modules can use them without referencing each other. Roles are mapped to
// policies in the composition root.
public static class Policies
{
    public const string CanManageJobs = "CanManageJobs";
    public const string CanViewJobs = "CanViewJobs";
    public const string CanManageUsers = "CanManageUsers";
    public const string CanManageTenant = "CanManageTenant";
    public const string CanViewApplications = "CanViewApplications";
    public const string CanManageApplications = "CanManageApplications";
    public const string CanViewInterviews = "CanViewInterviews";
    public const string CanManageInterviews = "CanManageInterviews";

    // Resource-based: checked through IAuthorizationService, not an attribute.
    public const string IsInterviewParticipant = "IsInterviewParticipant";

    // Both token kinds share a key and scheme, so this keeps company tokens out of candidate endpoints.
    public const string CandidateOnly = "CandidateOnly";
}

// Candidate tokens carry token_type=candidate; company tokens don't.
public static class TokenTypes
{
    public const string ClaimName = "token_type";
    public const string Candidate = "candidate";
}
