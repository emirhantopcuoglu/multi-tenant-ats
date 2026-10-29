using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Goes through the outbox, so a failure must propagate and roll back the save.
public sealed class PublishApplicationViewedIntegrationEvent
    : INotificationHandler<ApplicationViewedEvent>
{
    private readonly IPublishEndpoint _publishEndpoint;

    public PublishApplicationViewedIntegrationEvent(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task Handle(ApplicationViewedEvent notification, CancellationToken cancellationToken)
    {
        return _publishEndpoint.Publish(
            new ApplicationViewedIntegrationEvent(
                notification.ApplicationId,
                notification.JobId,
                notification.JobTitle,
                notification.CandidateId,
                notification.CandidateAccountId,
                notification.TenantId),
            cancellationToken);
    }
}
