using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Applications.Application.Events;

// Bridges the in-process domain event onto the message bus. MediatR delivers ApplicationSubmittedEvent
// synchronously inside the apply request (so that flow stays simple); this handler republishes it as
// an integration event on RabbitMQ, where the Notifications module consumes it out-of-process.
public sealed class PublishApplicationSubmittedIntegrationEvent
    : INotificationHandler<ApplicationSubmittedEvent>
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PublishApplicationSubmittedIntegrationEvent> _logger;

    public PublishApplicationSubmittedIntegrationEvent(
        IPublishEndpoint publishEndpoint,
        ILogger<PublishApplicationSubmittedIntegrationEvent> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(ApplicationSubmittedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await _publishEndpoint.Publish(
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
        catch (Exception ex)
        {
            // The application is already committed by the time this runs; a broker hiccup must not
            // fail the candidate's submission. Log and move on — the email is a best-effort side
            // effect, like the activity log and the cache. Durable publish (a transactional outbox)
            // arrives in Sprint 5.3 and closes this gap.
            _logger.LogWarning(
                ex,
                "Failed to publish ApplicationSubmittedIntegrationEvent for application {ApplicationId}",
                notification.ApplicationId);
        }
    }
}
