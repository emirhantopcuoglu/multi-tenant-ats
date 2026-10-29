using Ats.Shared.Kernel;

namespace Ats.Modules.Interviews.Application;

public static class InterviewErrors
{
    public static readonly Error ApplicationNotFound =
        new("interview.application_not_found", "The application to schedule an interview for was not found.");

    public static readonly Error ApplicationNotActive =
        new("interview.application_not_active", "Interviews can only be scheduled for an active application.");

    public static readonly Error NotFound =
        new("interview.not_found", "Interview not found.");

    public static Error InvalidOperation(string message) =>
        new("interview.invalid_operation", message);

    // 409: the request is valid but the interview's state or the clock no longer allows it.
    // The UI uses it to say the page is stale.
    public static Error TransitionNotAllowed(string message) =>
        new("interview.transition_not_allowed", message);

    public static readonly Error FeedbackNotEligible =
        new("interview.feedback_not_eligible", "Feedback can only be submitted once the interview has taken place.");

    public static readonly Error DuplicateFeedback =
        new("interview.duplicate_feedback", "Feedback has already been submitted by this interviewer for this interview.");

    public static readonly Error InterviewerConflict =
        new("interview.interviewer_conflict", "An interviewer already has another interview at this time.");

    public static readonly Error CandidateConflict =
        new("interview.candidate_conflict", "The candidate already has another interview at this time.");
}
