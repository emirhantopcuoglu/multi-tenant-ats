namespace Ats.Shared.Infrastructure;

// Connection settings for RabbitMQ, the message broker introduced in Sprint 5. MassTransit sits on
// top of it as the transport abstraction. Mirrors MongoOptions/RedisOptions: bound from the
// "RabbitMq" configuration section in Program.cs.
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; init; } = "localhost";

    public ushort Port { get; init; } = 5672;

    // The AMQP virtual host: a namespace for exchanges/queues. The default "/" is fine until we need
    // to isolate environments inside one broker.
    public string VirtualHost { get; init; } = "/";

    // In dev RabbitMQ runs with the credentials from docker-compose, so they live in appsettings.json
    // like the Postgres password. A shared/remote broker must override these via User Secrets /
    // environment variables.
    public string Username { get; init; } = "ats";

    public string Password { get; init; } = "ats_dev_password";
}
