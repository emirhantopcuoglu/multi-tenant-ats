using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Bridges the in-process domain event onto the message bus. MediatR delivers ApplicationSubmittedEvent
// synchronously inside the apply request; this handler republishes it as an integration event, which
// the Notifications module consumes out-of-process.
//
// With the transactional outbox enabled, IPublishEndpoint.Publish does not reach the broker here — it
// writes the message to the outbox tables in the same DbContext. The command handler publishes before
// its SaveChanges, so the message and the business rows commit atomically (or roll back together).
// That is why there is no try/catch: a failure must propagate and roll back, not be swallowed.
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
                notification.TenantId),
            cancellationToken);
    }
}
