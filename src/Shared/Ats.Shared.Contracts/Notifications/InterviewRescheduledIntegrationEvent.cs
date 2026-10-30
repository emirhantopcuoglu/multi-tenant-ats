namespace Ats.Shared.Contracts.Notifications;

// Self-contained. PreviousScheduledAtUtc lets consumers say what changed.
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
