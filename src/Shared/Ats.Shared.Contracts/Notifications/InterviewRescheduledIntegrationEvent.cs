namespace Ats.Shared.Contracts.Notifications;

// Published when a scheduled interview is moved to a new slot. Same self-contained shape as
// InterviewScheduledIntegrationEvent — everything a candidate-facing notification needs travels in
// the message, and the interview type is a string so the contract does not drag the Interviews
// module's enum across the boundary.
//
// PreviousScheduledAtUtc exists so consumers can say what changed rather than only what is now
// true: the candidate is holding the old time, and "your interview moved from X to Y" is the only
// version of this message that is actually actionable.
public sealed record InterviewRescheduledIntegrationEvent(
    Guid InterviewId,
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    Guid? CandidateAccountId,
    string CandidateEmail,
    string CandidateFirstName,
    string InterviewType,
    DateTime PreviousScheduledAtUtc,
    DateTime ScheduledAtUtc,
    int DurationMinutes,
    string? RoomToken,
    Guid TenantId);
