using Ats.Shared.Kernel;

namespace Ats.Modules.Applications.Application;

// Typed, structured failures returned via Result instead of thrown. The controller maps each
// code to an HTTP status, so the transport concern (404 vs 409) stays out of the handler.
public static class ApplicationErrors
{
    public static readonly Error TenantNotResolved =
        new("application.tenant_not_resolved", "The company could not be resolved from the URL.");

    public static readonly Error JobNotAvailable =
        new("application.job_not_available", "This job does not exist or is not open for applications.");

    public static readonly Error DuplicateApplication =
        new("application.duplicate", "An active application for this job already exists.");

    public static readonly Error NotFound =
        new("application.not_found", "Application not found.");

    public static readonly Error CandidateAccountNotFound =
        new("application.candidate_account_not_found", "The candidate account could not be found.");

    // The one gate on an unverified account. Applying is where an unreachable address stops being the
    // candidate's own problem and starts costing a recruiter real time, so this is the action that
    // waits for proof — not signing in, and not filling in a profile.
    public static readonly Error EmailNotVerified =
        new("application.email_not_verified",
            "Verify your email address before applying. Check your inbox for the confirmation link.");

    public static readonly Error CvNotParsed =
        new("application.cv_not_parsed", "The CV has not been parsed yet.");

    public static readonly Error StageNotInPipeline =
        new("application.stage_not_in_pipeline", "The target stage does not belong to this job's pipeline.");

    public static readonly Error CannotMoveBackward =
        new("application.cannot_move_backward",
            "An application cannot be moved to the same or an earlier stage. Use stage correction instead.");

    public static readonly Error TerminalStageRequiresDecision =
        new("application.terminal_stage_requires_decision",
            "Hired and Rejected are outcomes, not stages to move into. Use the hire or reject action instead.");

    // Withdrawal gets its own code rather than reusing InvalidOperation: this is the one lifecycle
    // failure a candidate can trigger themselves, so the message is written for them and the code is
    // stable enough for the portal to key a translated string on.
    public static readonly Error NotWithdrawable =
        new("application.not_withdrawable",
            "This application is already closed and cannot be withdrawn.");

    public static Error InvalidOperation(string message) =>
        new("application.invalid_operation", message);
}
