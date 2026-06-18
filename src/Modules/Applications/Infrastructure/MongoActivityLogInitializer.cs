using MongoDB.Driver;

namespace Ats.Modules.Applications.Infrastructure;

// Creates the index the activity-log read path relies on. Idempotent — MongoDB no-ops when an
// index with the same name and key spec already exists — so it is safe to run on every startup,
// mirroring FileStorageInitializer (the MinIO bucket) and RoleSeeder (the identity roles).
public static class MongoActivityLogInitializer
{
    // A stable, descriptive name makes the index recognizable in Compass and keeps re-creation a
    // no-op. (An unnamed index gets an auto-generated name from its keys, which is harder to read.)
    private const string ActivityLookupIndexName = "tenant_application_occurred_desc";

    public static async Task EnsureIndexesAsync(
        IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var collection = database.GetCollection<ActivityDocument>(
            MongoActivityLogRepository.CollectionName);

        // The only read is GetByApplicationAsync: it matches tenantId + applicationId for equality
        // and returns newest-first by occurredAtUtc. The ESR rule (Equality, Sort, Range) says to
        // order the keys equality-first, then the sort field — so this single compound index serves
        // both the filter and the sort, letting MongoDB return rows already ordered instead of
        // sorting them in memory. We deliberately add no other indexes (YAGNI): nothing queries
        // activityType or actorUserId yet, and every extra index slows inserts on this write-heavy,
        // append-only collection.
        var keys = Builders<ActivityDocument>.IndexKeys
            .Ascending(d => d.TenantId)
            .Ascending(d => d.ApplicationId)
            .Descending(d => d.OccurredAtUtc);

        var model = new CreateIndexModel<ActivityDocument>(
            keys, new CreateIndexOptions { Name = ActivityLookupIndexName });

        await collection.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
    }
}
