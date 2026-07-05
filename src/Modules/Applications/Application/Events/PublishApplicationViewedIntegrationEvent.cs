using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Bridges the in-process domain event onto the message bus, mirroring
// PublishApplicationStageChangedIntegrationEvent. MediatR delivers ApplicationViewedEvent
// synchronously inside the mark-viewed request; this handler republishes it for the notification
// backbone.
//
// With the transactional outbox enabled, IPublishEndpoint.Publish writes the message to the outbox
// tables in the same DbContext rather than reaching the broker. The command handler publishes before
// its SaveChanges, so the message and the view stamp commit atomically (or roll back together) —
// hence no try/catch: a failure must propagate, not be swallowed.
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
