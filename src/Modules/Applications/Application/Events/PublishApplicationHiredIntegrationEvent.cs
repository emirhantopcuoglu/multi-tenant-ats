using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Bridges the in-process domain event onto the message bus, mirroring
// PublishApplicationRejectedIntegrationEvent. With the transactional outbox enabled,
// IPublishEndpoint.Publish writes the message to the outbox tables in the same DbContext rather
// than reaching the broker. The command handler publishes before its SaveChanges, so the message
// and the hired status commit atomically — hence no try/catch: a failure must propagate.
public sealed class PublishApplicationHiredIntegrationEvent
    : INotificationHandler<ApplicationHiredEvent>
{
    private readonly IPublishEndpoint _publishEndpoint;

    public PublishApplicationHiredIntegrationEvent(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task Handle(ApplicationHiredEvent notification, CancellationToken cancellationToken)
    {
        return _publishEndpoint.Publish(
            new ApplicationHiredIntegrationEvent(
                notification.ApplicationId,
                notification.JobId,
                notification.JobTitle,
                notification.CandidateId,
                notification.CandidateEmail,
                notification.CandidateFirstName,
                notification.TenantId),
            cancellationToken);
    }
}
