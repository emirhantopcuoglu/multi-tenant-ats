using Ats.Modules.Interviews.Domain;
using MediatR;

namespace Ats.Modules.Interviews.Application.Events;

// Raised by the reminder sweep when a scheduled nudge falls due, and bridged onto RabbitMQ by
// PublishInterviewReminderDueIntegrationEvent — the same in-process/cross-process split the
// scheduled, rescheduled and cancelled events already use.
//
// Going through the in-process event rather than reaching for IBus in the job keeps every
// interview→bus mapping in one folder, and lets the sweep be tested against the same IPublisher
// double the command handlers use instead of a hand-rolled broker stub.
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
