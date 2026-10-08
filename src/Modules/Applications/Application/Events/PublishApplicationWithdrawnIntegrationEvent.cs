using Ats.Shared.Contracts.Applications;
using MassTransit;
using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Bridges the in-process withdrawal event onto the message bus, mirroring
// PublishApplicationRejectedIntegrationEvent.
//
// With the transactional outbox enabled, Publish writes to the outbox tables in the same DbContext
// instead of reaching the broker. The command handler publishes before its SaveChanges, so the
// message and the Withdrawn status commit atomically — hence no try/catch: a failure must propagate.
public sealed class PublishApplicationWithdrawnIntegrationEvent
    : INotificationHandler<ApplicationWithdrawnEvent>
{
    private readonly IPublishEndpoint _publishEndpoint;

    public PublishApplicationWithdrawnIntegrationEvent(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task Handle(ApplicationWithdrawnEvent notification, CancellationToken cancellationToken)
    {
        return _publishEndpoint.Publish(
            new ApplicationWithdrawnIntegrationEvent(
                notification.ApplicationId,
                notification.TenantId),
            cancellationToken);
    }
}
