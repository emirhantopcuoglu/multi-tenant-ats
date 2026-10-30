using Ats.Shared.Contracts.Applications;
using MassTransit;
using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Goes through the outbox, so the parse request commits with the application.
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
                notification.JobId,
                notification.CandidateId,
                notification.CvFileKey,
                notification.TenantId),
            cancellationToken);
    }
}
