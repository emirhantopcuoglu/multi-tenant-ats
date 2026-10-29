namespace Ats.Shared.Infrastructure;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    // No auth in dev. A connection string with credentials goes in User Secrets.
    public string ConnectionString { get; init; } = "mongodb://localhost:27017";

    public string DatabaseName { get; init; } = "ats";
}
