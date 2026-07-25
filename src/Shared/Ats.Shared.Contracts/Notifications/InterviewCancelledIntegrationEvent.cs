namespace Ats.Shared.Contracts.Notifications;

// Published when a scheduled interview is called off before it was due to start.
//
// Reason is a string from a closed set (see the Interviews module's InterviewCancellationReason)
// and is the whole point of carrying anything beyond the interview identity: it decides whether the
// candidate is told another invitation is coming or that this particular door has closed. The
// recruiter's free-text cancellation note is deliberately absent — internal wording must not reach
// a candidate-facing consumer, the same rule that keeps scheduling notes out of these contracts.
public sealed record InterviewCancelledIntegrationEvent(
    Guid InterviewId,
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    Guid? CandidateAccountId,
    string CandidateEmail,
    string CandidateFirstName,
    string InterviewType,
    DateTime ScheduledAtUtc,
    string Reason,
    Guid TenantId);
