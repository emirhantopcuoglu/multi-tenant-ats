namespace Ats.Shared.Infrastructure;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; init; } = string.Empty;

    public ushort Port { get; init; } = 5672;

    public string VirtualHost { get; init; } = "/";

    // Secret outside development: User Secrets or environment variables.
    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    // Exponential in-memory retry for consumers; after the last attempt the message goes to the
    // "_error" queue.
    public int RetryLimit { get; init; } = 5;

    public int RetryInitialIntervalSeconds { get; init; } = 2;

    public int RetryMaxIntervalSeconds { get; init; } = 30;
}
