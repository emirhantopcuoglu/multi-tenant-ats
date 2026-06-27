using MongoDB.Bson;
using MongoDB.Driver;

namespace Ats.Shared.Infrastructure;

// Creates indexes on audit_logs. Idempotent — MongoDB no-ops when the index already exists.
// Mirrors MongoActivityLogInitializer; run on every startup alongside the other initializers.
public static class MongoAuditLogInitializer
{
    public static async Task EnsureIndexesAsync(
        IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var collection = database.GetCollection<AuditLogDocument>(AuditLogInterceptor.CollectionName);

        // Two access patterns:
        // 1. "Show all changes for this tenant ordered by time" → (TenantId, OccurredAtUtc DESC)
        // 2. "Show all changes to a specific entity" → (TenantId, ResourceType, ResourceId, OccurredAtUtc DESC)
        // Index 2 covers index 1 as a prefix, but a dedicated index 1 is faster for the broad case.
        // ESR rule: equality keys first, then the sort field.
        var models = new[]
        {
            new CreateIndexModel<AuditLogDocument>(
                Builders<AuditLogDocument>.IndexKeys
                    .Ascending(d => d.TenantId)
                    .Descending(d => d.OccurredAtUtc),
                new CreateIndexOptions { Name = "tenant_occurred_desc" }),

            new CreateIndexModel<AuditLogDocument>(
                Builders<AuditLogDocument>.IndexKeys
                    .Ascending(d => d.TenantId)
                    .Ascending(d => d.ResourceType)
                    .Ascending(d => d.ResourceId)
                    .Descending(d => d.OccurredAtUtc),
                new CreateIndexOptions { Name = "tenant_resource_occurred_desc" })
        };

        await collection.Indexes.CreateManyAsync(models, cancellationToken: cancellationToken);
    }
}
