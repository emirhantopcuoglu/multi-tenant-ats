namespace Ats.Shared.Infrastructure;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    // No auth in dev. StackExchange.Redis syntax (host:port).
    public string ConnectionString { get; init; } = "localhost:6379";
}
