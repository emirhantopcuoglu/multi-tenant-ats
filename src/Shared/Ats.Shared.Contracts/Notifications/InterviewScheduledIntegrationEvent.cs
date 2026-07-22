namespace Ats.Shared.Contracts.Notifications;

// Published onto the message bus when a recruiter schedules an interview against an application.
// Nothing consumes it yet: the in-app notification writer and the interview-invitation email
// (later phases of the notification backbone) will subscribe to it.
//
// Self-contained like its siblings: everything a candidate-facing notification needs travels in
// the message. The interview type is a string, not the Interviews module's enum — a contract must
// not drag a module's domain types across the boundary. The recruiter's scheduling notes are
// deliberately absent: they are internal remarks, not something the candidate may ever see.
//
// CandidateAccountId is the global marketplace account the in-app notification is addressed to;
// nullable because applications submitted before candidate accounts existed have none — those
// messages produce no in-app notification, while email consumers keep working.
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
    string? Location,
    Guid TenantId);
