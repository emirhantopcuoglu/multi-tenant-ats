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
}
