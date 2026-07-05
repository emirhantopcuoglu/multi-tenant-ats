using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Raised in-process the first time a company user opens an application (MarkApplicationViewedHandler
// only fires it on the first view — see Application.MarkViewed). A handler in this module
// (PublishApplicationViewedIntegrationEvent) bridges it onto RabbitMQ for the in-app notification
// backbone. Same split as the other Applications events: the domain event stays inside the module,
// the integration event crosses it.
public sealed record ApplicationViewedEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    Guid? CandidateAccountId,
    Guid TenantId) : INotification;
