using Ats.Modules.Interviews.Domain;
using MediatR;

namespace Ats.Modules.Interviews.Application.Events;

// Raised in-process after an interview is moved to a new slot. Sibling of InterviewScheduledEvent
// and carries the same candidate contact facts for the same reason: the out-of-process consumer must
// not have to load another module's aggregates.
//
// PreviousScheduledAtUtc is the point of this event. A candidate already holds the old time — in
// their calendar, in the invitation email — so a notification that only states the new time leaves
// them to spot the difference themselves. The recruiter's notes stay out, as everywhere else.
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
