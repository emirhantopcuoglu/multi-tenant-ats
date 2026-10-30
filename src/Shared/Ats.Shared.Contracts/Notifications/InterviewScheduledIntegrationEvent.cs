namespace Ats.Shared.Contracts.Notifications;

// Self-contained. The type is a string so the contract doesn't carry the Interviews enum, and
// the recruiter's notes aren't included. CandidateAccountId is null for applications made before
// candidate accounts.
public sealed record InterviewScheduledIntegrationEvent(
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
    int DurationMinutes,
    // Null for phone screens. Not a secret; see Interview.RoomToken.
    string? RoomToken,
    Guid TenantId);
