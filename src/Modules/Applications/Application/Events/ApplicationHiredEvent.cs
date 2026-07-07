using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Raised in-process via MediatR after a recruiter hires a candidate. The positive counterpart of
// ApplicationRejectedEvent, with the same split: a handler in this module bridges it onto RabbitMQ
// as an integration event, which the Notifications module consumes to email the candidate.
//
// It carries plain data — ids plus the candidate name/email and job title the email needs — so the
// out-of-process consumer never loads this module's aggregates.
public sealed record ApplicationHiredEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    string CandidateEmail,
    string CandidateFirstName,
    Guid TenantId) : INotification;
