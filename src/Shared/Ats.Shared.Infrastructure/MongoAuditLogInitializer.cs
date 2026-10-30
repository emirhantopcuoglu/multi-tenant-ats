using MongoDB.Bson;
using MongoDB.Driver;

namespace Ats.Shared.Infrastructure;

// Creates the audit_logs indexes. Idempotent, so it runs on every startup.
public static class MongoAuditLogInitializer
{
    public static async Task EnsureIndexesAsync(
        IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var collection = database.GetCollection<AuditLogDocument>(AuditLogInterceptor.CollectionName);

        // Two reads: a tenant's changes by time, and one entity's changes by time.
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
