using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Raised in-process via MediatR after a recruiter rejects an application. A handler in this module
// (PublishApplicationRejectedIntegrationEvent) bridges it onto RabbitMQ, where the Notifications
// module consumes it to email the candidate. Same split as ApplicationSubmittedEvent: the domain
// event stays inside the module, the integration event crosses it.
//
// It carries plain data — ids plus the candidate name/email and job title the email needs — so the
// out-of-process consumer never loads this module's aggregates. It deliberately does NOT carry the
// rejection reason: that is the recruiter's internal note, not something the candidate is shown.
public sealed record ApplicationRejectedEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    string CandidateEmail,
    string CandidateFirstName,
    Guid TenantId) : INotification;
