using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Domain;
using Ats.Shared.Kernel;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Ats.Modules.Applications.Infrastructure;

// MongoDB-backed activity log. Uses the native MongoDB.Driver (no EF Core provider): the log is
// schema-flexible, append-only and write-heavy, which is what a document store is good at.
//
// Two responsibilities that EF used to handle for free are now explicit here:
//  1. Tenant stamping — the TenantSaveChangesInterceptor set TenantId on insert; here the
//     repository does it from ICurrentTenant.
//  2. Tenant filtering — the global query filter added "WHERE TenantId = current" to every read;
//     here every query pairs applicationId with tenantId by hand. Forgetting it is a data leak,
//     so it is funnelled through this single type.
public sealed class MongoActivityLogRepository : IActivityLogRepository
{
    // Snake-case to match MongoDB collection-naming convention; lower-case keeps it shell-friendly.
    private const string CollectionName = "application_activities";

    private readonly IMongoCollection<ActivityDocument> _collection;
    private readonly ICurrentTenant _currentTenant;

    public MongoActivityLogRepository(IMongoDatabase database, ICurrentTenant currentTenant)
    {
        _collection = database.GetCollection<ActivityDocument>(CollectionName);
        _currentTenant = currentTenant;
    }

    public async Task AddAsync(ApplicationActivity activity, CancellationToken cancellationToken = default)
    {
        // A log entry with no tenant could never be read back safely (every read is tenant-scoped),
        // so refuse to write one. The caller only reaches here inside a resolved-tenant request.
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException(
                "Cannot write an activity log entry without a resolved tenant.");

        var document = new ActivityDocument
        {
            Id = activity.Id,
            TenantId = tenantId,
            ApplicationId = activity.ApplicationId,
            ActivityType = activity.ActivityType.ToString(),
            ActorUserId = activity.ActorUserId,
            // The domain holds the payload as a JSON string; parse it into a real sub-document so
            // it is stored (and later queryable) as structured BSON rather than an opaque string.
            Payload = BsonDocument.Parse(activity.Payload),
            OccurredAtUtc = activity.OccurredAtUtc
        };

        await _collection.InsertOneAsync(document, options: null, cancellationToken);
    }

    public async Task<IReadOnlyList<ActivityLogEntry>> GetByApplicationAsync(
        Guid applicationId, CancellationToken cancellationToken = default)
    {
        // No tenant means no safe scope to read in — return nothing rather than leak across tenants.
        if (_currentTenant.TenantId is not { } tenantId)
            return [];

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
