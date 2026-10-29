using Ats.Modules.Interviews.Domain;
using MediatR;

namespace Ats.Modules.Interviews.Application.Events;

// Carries everything the consumer needs, so it never loads another module's data. No recruiter
// notes. CandidateAccountId is null for applications made before candidate accounts.
public sealed record InterviewScheduledEvent(
    Guid InterviewId,
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    Guid? CandidateAccountId,
    string CandidateEmail,
    string CandidateFirstName,
    InterviewType Type,
    DateTime ScheduledAtUtc,
    int DurationMinutes,
    string? RoomToken,
    Guid TenantId) : INotification;
