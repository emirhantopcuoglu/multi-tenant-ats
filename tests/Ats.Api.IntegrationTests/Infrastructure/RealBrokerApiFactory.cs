using Testcontainers.RabbitMq;

namespace Ats.Api.IntegrationTests.Infrastructure;

// The app's own bus configuration against a real RabbitMQ, for what the in-memory harness can't show:
// that the exchanges and queues get created and a message makes it through the broker.
public sealed class RealBrokerApiFactory : AtsApiFactory
{
    private const string Username = "ats";
    private const string Password = "integration-tests";

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-management")
        .WithUsername(Username)
        .WithPassword(Password)
        .Build();

    protected override bool UsesRealBroker => true;

    public override async Task InitializeAsync()
    {
        await _rabbitMq.StartAsync();
        await base.InitializeAsync();
    }

    protected override async Task DisposeContainersAsync()
    {
        await base.DisposeContainersAsync();
        await _rabbitMq.DisposeAsync();
    }

    protected override Dictionary<string, string> BuildSettings()
    {
        var settings = base.BuildSettings();
        settings["RabbitMq:Host"] = _rabbitMq.Hostname;
        settings["RabbitMq:Port"] = _rabbitMq.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort).ToString();
        settings["RabbitMq:Username"] = Username;
        settings["RabbitMq:Password"] = Password;
        return settings;
    }
}

[CollectionDefinition(Name)]
public sealed class RealBrokerTestCollection : ICollectionFixture<RealBrokerApiFactory>
{
    public const string Name = "RealBroker";
}
