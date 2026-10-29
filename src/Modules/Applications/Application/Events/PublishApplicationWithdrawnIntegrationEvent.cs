using Ats.Shared.Contracts.Applications;
using MassTransit;
using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Goes through the outbox, so a failure must propagate and roll back the save.
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
