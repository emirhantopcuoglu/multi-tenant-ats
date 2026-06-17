namespace Ats.Shared.Infrastructure;

// Connection settings for MongoDB, which from Sprint 4 holds the append-only activity log.
// Mirrors FileStorageOptions: bound from the "Mongo" configuration section in Program.cs.
public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    // In dev MongoDB runs without authentication (see docker-compose), so the connection string
    // carries no secret and can live in appsettings.json like the Postgres one. A connection
    // string with credentials belongs in User Secrets / environment variables instead.
    public string ConnectionString { get; init; } = "mongodb://localhost:27017";

    public string DatabaseName { get; init; } = "ats";
}
