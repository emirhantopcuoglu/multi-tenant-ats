using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Goes through the outbox, so a failure must propagate and roll back the save.
public sealed class PublishApplicationStageChangedIntegrationEvent
    : INotificationHandler<ApplicationStageChangedEvent>
{
    private readonly IPublishEndpoint _publishEndpoint;

    public PublishApplicationStageChangedIntegrationEvent(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task Handle(ApplicationStageChangedEvent notification, CancellationToken cancellationToken)
    {
        return _publishEndpoint.Publish(
            new ApplicationStageChangedIntegrationEvent(
                notification.ApplicationId,
                notification.JobId,
                notification.JobTitle,
                notification.CandidateId,
                notification.CandidateAccountId,
                notification.CandidateEmail,
                notification.CandidateFirstName,
                notification.FromStageId,
                notification.FromStageName,
                notification.ToStageId,
                notification.ToStageName,
                notification.TenantId),
            cancellationToken);
    }
}
