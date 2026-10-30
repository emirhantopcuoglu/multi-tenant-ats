using Ats.Modules.Interviews.Domain;
using MediatR;

namespace Ats.Modules.Interviews.Application.Events;

// Carries the Reason but not the recruiter's CancellationNote, so no consumer can leak it.
public sealed record InterviewCancelledEvent(
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
    InterviewCancellationReason Reason,
    Guid TenantId) : INotification;
