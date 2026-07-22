using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Bridges the in-process domain event onto the message bus, mirroring
// PublishApplicationViewedIntegrationEvent. Publish happens before SaveChanges in the command
// handler, so the message and the download stamp commit atomically through the transactional
// outbox — hence no try/catch: a failure must propagate, not be swallowed.
public sealed class PublishApplicationCvDownloadedIntegrationEvent
    : INotificationHandler<ApplicationCvDownloadedEvent>
{
    private readonly IPublishEndpoint _publishEndpoint;

    public PublishApplicationCvDownloadedIntegrationEvent(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task Handle(ApplicationCvDownloadedEvent notification, CancellationToken cancellationToken)
    {
        return _publishEndpoint.Publish(
            new ApplicationCvDownloadedIntegrationEvent(
                notification.ApplicationId,
                notification.JobId,
                notification.JobTitle,
                notification.CandidateId,
                notification.CandidateAccountId,
                notification.TenantId),
            cancellationToken);
    }
}
