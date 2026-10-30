using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;

namespace Ats.Modules.Interviews.Application.Events;

// Doesn't use IntegrationEventBridge: delivering the reminder is the whole job, so a broker
// failure should fail the sweep and let Hangfire retry. Consumers dedupe on (interview, kind).
public sealed class PublishInterviewReminderDueIntegrationEvent
    : INotificationHandler<InterviewReminderDueEvent>
{
    private readonly IBus _bus;

    public PublishInterviewReminderDueIntegrationEvent(IBus bus) => _bus = bus;

    public Task Handle(InterviewReminderDueEvent notification, CancellationToken cancellationToken) =>
        _bus.Publish(
            new InterviewReminderDueIntegrationEvent(
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
                notification.DurationMinutes,
                notification.RoomToken,
                notification.Kind.ToString(),
                notification.TenantId),
            cancellationToken);
}
