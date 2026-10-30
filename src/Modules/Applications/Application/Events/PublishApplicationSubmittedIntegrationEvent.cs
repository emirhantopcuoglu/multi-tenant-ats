using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// IPublishEndpoint writes to the outbox tables in the same DbContext here, not to the broker.
// The handler publishes before SaveChanges, so a failure must propagate and roll back.
public sealed class PublishApplicationSubmittedIntegrationEvent
    : INotificationHandler<ApplicationSubmittedEvent>
{
    private readonly IPublishEndpoint _publishEndpoint;

    public PublishApplicationSubmittedIntegrationEvent(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task Handle(ApplicationSubmittedEvent notification, CancellationToken cancellationToken)
    {
        return _publishEndpoint.Publish(
            new ApplicationSubmittedIntegrationEvent(
                notification.ApplicationId,
                notification.JobId,
                notification.JobTitle,
                notification.CandidateId,
                notification.CandidateEmail,
                notification.CandidateFirstName,
                notification.CandidateLastName,
                notification.TenantId),
            cancellationToken);
    }
}
