using Ats.Shared.Contracts.Applications;
using MassTransit;
using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Bridges the in-process CvParseRequestedEvent onto the message bus as an integration event, which
// the CV-parsing consumer handles out-of-process. Identical shape to
// PublishApplicationSubmittedIntegrationEvent: with the transactional outbox enabled,
// IPublishEndpoint.Publish writes to the outbox tables in the same DbContext as the application
// insert, so the parse request and the application row commit atomically — no try/catch, a failure
// must roll both back.
public sealed class PublishCvParseRequestedIntegrationEvent
    : INotificationHandler<CvParseRequestedEvent>
{
    private readonly IPublishEndpoint _publishEndpoint;

    public PublishCvParseRequestedIntegrationEvent(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task Handle(CvParseRequestedEvent notification, CancellationToken cancellationToken)
    {
        return _publishEndpoint.Publish(
            new CvParseRequestedIntegrationEvent(
                notification.ApplicationId,
                notification.CandidateId,
                notification.CvFileKey,
                notification.TenantId),
            cancellationToken);
    }
}
