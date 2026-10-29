using Ats.Modules.Interviews.Domain;
using MediatR;

namespace Ats.Modules.Interviews.Application.Events;

// Raised by the reminder sweep. Going through MediatR keeps the bus mapping with the other bridges
// and lets tests use the same IPublisher double.
public sealed record InterviewReminderDueEvent(
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
    InterviewReminderKind Kind,
    Guid TenantId) : INotification;
