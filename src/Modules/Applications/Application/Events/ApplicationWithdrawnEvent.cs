using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Raised in-process after a candidate withdraws their own application, and bridged onto RabbitMQ by
// PublishApplicationWithdrawnIntegrationEvent. Same split as ApplicationRejectedEvent: the in-process
// event stays inside the module, the integration event crosses the boundary.
//
// Carries no candidate contact details, unlike the rejected and hired events — nothing downstream
// emails anybody here. See ApplicationWithdrawnIntegrationEvent for why.
public sealed record ApplicationWithdrawnEvent(
    Guid ApplicationId,
    Guid TenantId) : INotification;
