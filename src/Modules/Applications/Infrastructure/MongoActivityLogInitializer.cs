using MongoDB.Driver;

namespace Ats.Modules.Applications.Infrastructure;

// Creates the activity log index. Idempotent, so it runs on every startup.
public static class MongoActivityLogInitializer
{
    private const string ActivityLookupIndexName = "tenant_application_occurred_desc";

    public static async Task EnsureIndexesAsync(
        IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var collection = database.GetCollection<ActivityDocument>(
            MongoActivityLogRepository.CollectionName);

        // Serves GetByApplicationAsync: equality on tenantId and applicationId, then sort by
        // occurredAtUtc. No other indexes, since nothing else is queried.
        var keys = Builders<ActivityDocument>.IndexKeys
            .Ascending(d => d.TenantId)
            .Ascending(d => d.ApplicationId)
            .Descending(d => d.OccurredAtUtc);

        var model = new CreateIndexModel<ActivityDocument>(
            keys, new CreateIndexOptions { Name = ActivityLookupIndexName });

        await collection.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
    }
}
