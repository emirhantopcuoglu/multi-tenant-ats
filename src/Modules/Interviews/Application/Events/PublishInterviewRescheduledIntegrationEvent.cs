using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Interviews.Application.Events;

// Bridges "this interview moved" onto the bus. See IntegrationEventBridge for the transport and
// failure policy shared with the other bridges in this module.
public sealed class PublishInterviewRescheduledIntegrationEvent
    : INotificationHandler<InterviewRescheduledEvent>
{
    private readonly IBus _bus;
    private readonly ILogger<PublishInterviewRescheduledIntegrationEvent> _logger;

    public PublishInterviewRescheduledIntegrationEvent(
        IBus bus, ILogger<PublishInterviewRescheduledIntegrationEvent> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    public Task Handle(InterviewRescheduledEvent notification, CancellationToken cancellationToken) =>
        IntegrationEventBridge.PublishOrLogAsync(
            _bus,
            new InterviewRescheduledIntegrationEvent(
                notification.InterviewId,
                notification.ApplicationId,
                notification.JobId,
                notification.JobTitle,
                notification.CandidateId,
                notification.CandidateAccountId,
                notification.CandidateEmail,
                notification.CandidateFirstName,
                notification.Type.ToString(),
                notification.PreviousScheduledAtUtc,
                notification.ScheduledAtUtc,
                notification.DurationMinutes,
                notification.RoomToken,
                notification.TenantId),
            _logger,
            notification.InterviewId,
            notification.ApplicationId,
            cancellationToken);
}
