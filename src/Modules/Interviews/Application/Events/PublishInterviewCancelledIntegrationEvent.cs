using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Interviews.Application.Events;

// The reason crosses as a string so the contract doesn't carry this module's enum.
public sealed class PublishInterviewCancelledIntegrationEvent
    : INotificationHandler<InterviewCancelledEvent>
{
    private readonly IBus _bus;
    private readonly ILogger<PublishInterviewCancelledIntegrationEvent> _logger;

    public PublishInterviewCancelledIntegrationEvent(
        IBus bus, ILogger<PublishInterviewCancelledIntegrationEvent> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    public Task Handle(InterviewCancelledEvent notification, CancellationToken cancellationToken) =>
        IntegrationEventBridge.PublishOrLogAsync(
            _bus,
            new InterviewCancelledIntegrationEvent(
                notification.InterviewId,
                notification.ApplicationId,
                notification.JobId,
                notification.JobTitle,
                notification.CandidateId,
                notification.CandidateAccountId,
                notification.CandidateEmail,
                notification.CandidateFirstName,
                notification.Type.ToString(),
                notification.ScheduledAtUtc,
                notification.Reason.ToString(),
                notification.TenantId),
            _logger,
            notification.InterviewId,
            notification.ApplicationId,
            cancellationToken);
}
