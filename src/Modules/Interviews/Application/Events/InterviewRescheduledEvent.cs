using Ats.Modules.Interviews.Domain;
using MediatR;

namespace Ats.Modules.Interviews.Application.Events;

// Includes the previous time so the candidate can see what changed. No recruiter notes.
public sealed record InterviewRescheduledEvent(
    Guid InterviewId,
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    Guid? CandidateAccountId,
    string CandidateEmail,
    string CandidateFirstName,
    InterviewType Type,
    DateTime PreviousScheduledAtUtc,
    DateTime ScheduledAtUtc,
    int DurationMinutes,
    string? RoomToken,
    Guid TenantId) : INotification;
