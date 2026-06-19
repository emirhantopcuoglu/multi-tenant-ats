namespace Ats.Shared.Infrastructure;

// Connection settings for Redis, which from Sprint 4 backs the distributed cache (IDistributedCache).
// Mirrors MongoOptions/FileStorageOptions: bound from the "Redis" configuration section in Program.cs.
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    // In dev Redis runs without authentication (see docker-compose), so the connection string
    // carries no secret and can live in appsettings.json. A credentialed string belongs in
    // User Secrets / environment variables. Format is the StackExchange.Redis syntax (host:port).
    public string ConnectionString { get; init; } = "localhost:6379";
}
