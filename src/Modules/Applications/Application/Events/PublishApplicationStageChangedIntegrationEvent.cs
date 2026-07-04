using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Bridges the in-process domain event onto the message bus, mirroring
// PublishApplicationRejectedIntegrationEvent. MediatR delivers ApplicationStageChangedEvent
// synchronously inside the move-stage request; this handler republishes it as an integration
// event for the notification backbone.
//
// With the transactional outbox enabled, IPublishEndpoint.Publish writes the message to the outbox
// tables in the same DbContext rather than reaching the broker. The command handler publishes before
// its SaveChanges, so the message and the stage move commit atomically (or roll back together) —
// hence no try/catch: a failure must propagate, not be swallowed.
public sealed class PublishApplicationStageChangedIntegrationEvent
    : INotificationHandler<ApplicationStageChangedEvent>
{
    private readonly IPublishEndpoint _publishEndpoint;

    public PublishApplicationStageChangedIntegrationEvent(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task Handle(ApplicationStageChangedEvent notification, CancellationToken cancellationToken)
    {
        return _publishEndpoint.Publish(
            new ApplicationStageChangedIntegrationEvent(
                notification.ApplicationId,
                notification.JobId,
                notification.JobTitle,
                notification.CandidateId,
                notification.CandidateEmail,
                notification.CandidateFirstName,
                notification.FromStageId,
                notification.FromStageName,
                notification.ToStageId,
                notification.ToStageName,
                notification.TenantId),
            cancellationToken);
    }
}
