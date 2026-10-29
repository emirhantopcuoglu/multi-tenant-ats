using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Interviews.Application.Events;

// Shared publish policy for the interview bridges. Uses IBus, not IPublishEndpoint: the only bus
// outbox belongs to the Applications context, so a scoped publish from here would be dropped.
// Callers publish after SaveChanges, and broker failures are logged instead of failing a request
// whose change is already saved.
internal static class IntegrationEventBridge
{
    public static async Task PublishOrLogAsync<TIntegrationEvent>(
        IBus bus,
        TIntegrationEvent integrationEvent,
        ILogger logger,
        Guid interviewId,
        Guid applicationId,
        CancellationToken cancellationToken)
        where TIntegrationEvent : class
    {
        try
        {
            await bus.Publish(integrationEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish {EventType} for interview {InterviewId} (application {ApplicationId}); " +
                "the change is committed but no notification will go out",
                typeof(TIntegrationEvent).Name,
                interviewId,
                applicationId);
        }
    }
}
