using Ats.Api.IntegrationTests.Infrastructure;
using Ats.Modules.Notifications.Infrastructure;
using Ats.Shared.Contracts.Notifications;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ats.Api.IntegrationTests.Messaging;

[Collection(RealBrokerTestCollection.Name)]
public sealed class RabbitMqRoundTripTests
{
    private readonly RealBrokerApiFactory _factory;

    public RabbitMqRoundTripTests(RealBrokerApiFactory factory) => _factory = factory;

    [Fact]
    public async Task a_published_event_should_reach_its_consumer_through_rabbitmq()
    {
        var candidateAccountId = Guid.NewGuid();
        // Published before the consumer's queue is bound, the message would have nowhere to go.
        var bus = _factory.Services.GetRequiredService<IBusControl>();
        Assert.Equal(BusHealthStatus.Healthy, await bus.WaitForHealthStatus(BusHealthStatus.Healthy, TimeSpan.FromSeconds(60)));

        await bus.Publish(new ApplicationViewedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), "Data Engineer", Guid.NewGuid(), candidateAccountId, Guid.NewGuid()));

        Assert.True(await NotificationArrivesAsync(candidateAccountId));
    }

    private async Task<bool> NotificationArrivesAsync(Guid recipientId)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            if (await db.Notifications.AnyAsync(n => n.RecipientId == recipientId))
                return true;
            await Task.Delay(500);
        }

        return false;
    }
}
