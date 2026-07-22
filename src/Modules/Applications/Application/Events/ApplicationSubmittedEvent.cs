using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// A domain event raised in-process via MediatR after an application is persisted. A handler in
// this module (PublishApplicationSubmittedIntegrationEvent) bridges it onto RabbitMQ as an
// integration event, where the Notifications module consumes it to send the candidate's
// confirmation email. Keeping the domain event in-process and the integration event on the bus
// is the standard split: domain events stay inside the module, integration events cross it.
//
// It carries plain data (ids plus the candidate name/email and job title needed for the email) —
// no entity references — so the out-of-process consumer can handle it without loading this
// module's aggregates.
public sealed record ApplicationSubmittedEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    string CandidateEmail,
    string CandidateFirstName,
    string CandidateLastName,
    Guid TenantId) : INotification;
