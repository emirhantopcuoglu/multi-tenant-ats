using Ats.Modules.Interviews.Domain;
using MediatR;

namespace Ats.Modules.Interviews.Application.Events;

// Raised in-process after a recruiter schedules an interview. A handler in this module
// (PublishInterviewScheduledIntegrationEvent) bridges it onto RabbitMQ — the same split the
// Applications module uses: the domain event stays inside the module, the integration event
// crosses it.
//
// It carries plain data: the interview facts from the new aggregate plus the candidate contact and
// job title that arrived through IApplicationDirectory, so the out-of-process consumer never loads
// another module's aggregates. The recruiter's notes are deliberately not here — they are internal
// and must never feed a candidate-facing notification. CandidateAccountId is the global marketplace
// account an in-app notification is addressed to (nullable: pre-account applications have none).
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
    string? Location,
    Guid TenantId) : INotification;
