using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Ats.Shared.Infrastructure;

public sealed class AuditLogInterceptor : SaveChangesInterceptor
{
    internal const string CollectionName = "audit_logs";

    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMongoDatabase _database;

    private List<AuditLogDocument>? _pendingDocuments;

    public AuditLogInterceptor(
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IHttpContextAccessor httpContextAccessor,
        IMongoDatabase database)
    {
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
        _database = database;
    }

    // Capture before/after state here, while OriginalValues still holds the pre-save data.
    // AuditableSaveChangesInterceptor runs first (registration order) and has already converted
    // EntityState.Deleted → EntityState.Modified + set IsDeleted = true on soft-deletable entities.
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        _pendingDocuments = BuildDocuments(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    // Write to MongoDB after the DB commit so a DB failure leaves no orphan audit entries.
    // Best-effort: if MongoDB is unreachable the audit write fails silently — the DB change
    // has already committed. Acceptable for an internal audit trail; add alerting if needed.
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken ct = default)
    {
        if (_pendingDocuments is { Count: > 0 })
        {
            try
            {
                var collection = _database.GetCollection<AuditLogDocument>(CollectionName);
                await collection.InsertManyAsync(_pendingDocuments, cancellationToken: ct);
            }
            catch
            {
                // Best-effort: do not let an audit failure roll back a committed DB change.
            }
            finally
            {
                _pendingDocuments = null;
            }
        }

        return await base.SavedChangesAsync(eventData, result, ct);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken ct = default)
    {
        // DB save failed — discard captured entries so they are not written on a retry.
        _pendingDocuments = null;
        return base.SaveChangesFailedAsync(eventData, ct);
    }

    private List<AuditLogDocument> BuildDocuments(DbContext? context)
    {
        if (context is null || _currentTenant.TenantId is not { } tenantId)
            return [];

        var now = DateTime.UtcNow;
        var actorUserId = _currentUser.UserId;
        var actorEmail = _currentUser.Email;
        var httpCtx = _httpContextAccessor.HttpContext;
        var ipAddress = httpCtx?.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpCtx?.Request.Headers.UserAgent.ToString();

        var documents = new List<AuditLogDocument>();

        foreach (var entry in context.ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var resourceId = entry.Property("Id").CurrentValue?.ToString() ?? string.Empty;
            var resourceType = entry.Entity.GetType().Name;

            BsonDocument? before = null;
            BsonDocument? after = null;
            string action;

            if (entry.State == EntityState.Added)
            {
                action = "Created";
                after = BuildSnapshot(entry.CurrentValues);
            }
            else if (entry.State == EntityState.Deleted)
            {
                action = "Deleted";
                before = BuildSnapshot(entry.OriginalValues);
            }
            else // Modified — includes soft deletes (AuditableSaveChangesInterceptor converted them)
            {
                // Detect soft delete: IsDeleted just became true.
                var isSoftDelete = entry.Entity is ISoftDeletable
                    && entry.Property(nameof(ISoftDeletable.IsDeleted)).IsModified
                    && entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue is true;

                action = isSoftDelete ? "Deleted" : "Updated";

                var changedProps = entry.Properties.Where(p => p.IsModified).ToList();
                if (changedProps.Count == 0)
                    continue;

                before = BuildDiff(changedProps, useOriginal: true);
                after = BuildDiff(changedProps, useOriginal: false);
            }

            documents.Add(new AuditLogDocument
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                ActorEmail = actorEmail,
                Action = action,
                ResourceType = resourceType,
                ResourceId = resourceId,
                Before = before,
                After = after,
                OccurredAtUtc = now,
                IpAddress = ipAddress,
                UserAgent = userAgent
            });
        }

        return documents;
    }

    private static BsonDocument BuildSnapshot(PropertyValues values)
    {
        var doc = new BsonDocument();
        foreach (var prop in values.Properties)
            doc[prop.Name] = ToBsonSafe(values[prop.Name]);
        return doc;
    }

    private static BsonDocument BuildDiff(List<PropertyEntry> properties, bool useOriginal)
    {
        var doc = new BsonDocument();
        foreach (var prop in properties)
            doc[prop.Metadata.Name] = ToBsonSafe(useOriginal ? prop.OriginalValue : prop.CurrentValue);
        return doc;
    }

    private static BsonValue ToBsonSafe(object? value) => value switch
    {
        null => BsonNull.Value,
        bool b => new BsonBoolean(b),
        int i => new BsonInt32(i),
        long l => new BsonInt64(l),
        double d => new BsonDouble(d),
        decimal dec => new BsonDecimal128(dec),
        DateTime dt => new BsonDateTime(dt),
        Guid g => new BsonString(g.ToString()),
        _ => new BsonString(value.ToString() ?? string.Empty)
    };
}
