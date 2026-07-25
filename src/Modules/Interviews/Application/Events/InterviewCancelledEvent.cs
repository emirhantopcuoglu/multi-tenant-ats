using Ats.Modules.Interviews.Domain;
using MediatR;

namespace Ats.Modules.Interviews.Application.Events;

// Raised in-process after a scheduled interview is called off.
//
// Carries the structured Reason but never Interview.CancellationNote: the reason selects the
// candidate-facing sentence, while the note is the recruiter's internal wording. Keeping the note
// off the event means no downstream consumer can leak it by accident — the same structural
// safeguard that keeps Notes out of InterviewScheduledEvent.
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
