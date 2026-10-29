namespace Ats.Shared.Infrastructure;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; init; } = "localhost";

    public ushort Port { get; init; } = 5672;

    public string VirtualHost { get; init; } = "/";

    // Dev credentials from docker-compose. A shared broker overrides these with User Secrets.
    public string Username { get; init; } = "ats";

    public string Password { get; init; } = "ats_dev_password";

    // Exponential in-memory retry for consumers; after the last attempt the message goes to the
    // "_error" queue.
    public int RetryLimit { get; init; } = 5;

    public int RetryInitialIntervalSeconds { get; init; } = 2;

    public int RetryMaxIntervalSeconds { get; init; } = 30;
}
