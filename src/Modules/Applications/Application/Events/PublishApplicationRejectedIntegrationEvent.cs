using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Goes through the outbox, so a failure must propagate and roll back the save.
public sealed class PublishApplicationRejectedIntegrationEvent
    : INotificationHandler<ApplicationRejectedEvent>
{
    private readonly IPublishEndpoint _publishEndpoint;

    public PublishApplicationRejectedIntegrationEvent(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task Handle(ApplicationRejectedEvent notification, CancellationToken cancellationToken)
    {
        return _publishEndpoint.Publish(
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
}
