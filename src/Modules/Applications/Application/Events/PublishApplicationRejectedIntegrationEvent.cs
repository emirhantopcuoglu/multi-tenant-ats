using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Applications.Application.Events;

// Bridges the in-process domain event onto the message bus, mirroring
// PublishApplicationSubmittedIntegrationEvent. MediatR delivers ApplicationRejectedEvent
// synchronously inside the reject request; this handler republishes it as an integration event on
// RabbitMQ, where the Notifications module consumes it out-of-process.
public sealed class PublishApplicationRejectedIntegrationEvent
    : INotificationHandler<ApplicationRejectedEvent>
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PublishApplicationRejectedIntegrationEvent> _logger;

    public PublishApplicationRejectedIntegrationEvent(
        IPublishEndpoint publishEndpoint,
        ILogger<PublishApplicationRejectedIntegrationEvent> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(ApplicationRejectedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await _publishEndpoint.Publish(
                new ApplicationRejectedIntegrationEvent(
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
            // The rejection is already committed by the time this runs; a broker hiccup must not
            // fail the recruiter's action. Log and move on — the email is a best-effort side effect,
            // like the activity log and the cache. A transactional outbox closes this gap later.
            _logger.LogWarning(
                ex,
                "Failed to publish ApplicationRejectedIntegrationEvent for application {ApplicationId}",
                notification.ApplicationId);
        }
    }
}
