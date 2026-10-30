namespace Ats.Shared.Contracts.Notifications;

// Reason (from a closed set) decides whether the candidate is told a new invitation is coming.
// The recruiter's note isn't included.
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
