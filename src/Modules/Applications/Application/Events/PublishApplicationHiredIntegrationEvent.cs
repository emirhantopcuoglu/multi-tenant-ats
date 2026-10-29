using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Goes through the outbox, so a failure must propagate and roll back the save.
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
