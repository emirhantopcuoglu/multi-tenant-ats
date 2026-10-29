using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Domain;
using Ats.Shared.Kernel;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Ats.Modules.Applications.Infrastructure;

// Mongo has no tenant filter or interceptor, so this type stamps the tenant on writes and
// filters by it on every read.
public sealed class MongoActivityLogRepository : IActivityLogRepository
{
    // Internal so MongoActivityLogInitializer uses the same name.
    internal const string CollectionName = "application_activities";

    private readonly IMongoCollection<ActivityDocument> _collection;
    private readonly ICurrentTenant _currentTenant;

    public MongoActivityLogRepository(IMongoDatabase database, ICurrentTenant currentTenant)
    {
        _collection = database.GetCollection<ActivityDocument>(CollectionName);
        _currentTenant = currentTenant;
    }

    public Task AddAsync(ApplicationActivity activity, CancellationToken cancellationToken = default)
    {
        // A document without a tenant could never be read back safely.
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException(
                "Cannot write an activity log entry without a resolved tenant.");

        return AddAsync(activity, tenantId, cancellationToken);
    }

    public async Task AddAsync(
        ApplicationActivity activity, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant id is required.", nameof(tenantId));

        var document = new ActivityDocument
        {
            Id = activity.Id,
            TenantId = tenantId,
            ApplicationId = activity.ApplicationId,
            ActivityType = activity.ActivityType.ToString(),
            ActorUserId = activity.ActorUserId,
            // Stored as a real sub-document, not a string.
            Payload = BsonDocument.Parse(activity.Payload),
            OccurredAtUtc = activity.OccurredAtUtc
        };

        await _collection.InsertOneAsync(document, options: null, cancellationToken);
    }

    public async Task<IReadOnlyList<ActivityLogEntry>> GetByApplicationAsync(
        Guid applicationId, CancellationToken cancellationToken = default)
    {
        // No tenant, no results.
        if (_currentTenant.TenantId is not { } tenantId)
            return [];

        return await GetByApplicationAsync(applicationId, tenantId, cancellationToken);
    }

    public async Task<IReadOnlyList<ActivityLogEntry>> GetByApplicationAsync(
        Guid applicationId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ActivityDocument>.Filter.Eq(d => d.TenantId, tenantId)
            & Builders<ActivityDocument>.Filter.Eq(d => d.ApplicationId, applicationId);

        var documents = await _collection
            .Find(filter)
            .SortByDescending(d => d.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        return documents
            .Select(d => new ActivityLogEntry(
                d.Id,
                d.ApplicationId,
                d.ActivityType,
                d.ActorUserId,
                d.Payload.ToJson(),
                d.OccurredAtUtc))
            .ToList();
    }
}
