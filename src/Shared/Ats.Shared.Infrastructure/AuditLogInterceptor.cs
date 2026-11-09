using System.Text.Json;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ats.Shared.Infrastructure;

// Adds the audit rows to the same SaveChanges, so a change and its audit row commit or roll back
// together. Runs after AuditableSaveChangesInterceptor, which has already turned deletes into soft
// deletes, and while OriginalValues still holds the old data.
public sealed class AuditLogInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogInterceptor(
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IHttpContextAccessor httpContextAccessor)
    {
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        AddAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AddAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void AddAuditEntries(DbContext? context)
    {
        if (context is null || _currentTenant.TenantId is not { } tenantId)
            return;

        // Listed first: adding while enumerating the change tracker would change it.
        var tracked = context.ChangeTracker.Entries().ToList();
        var owned = tracked.Where(entry => entry.Metadata.IsOwned()).ToList();
        var auditEntries = tracked
            .Where(entry => entry.Entity is ITenantScoped)
            .Select(entry => BuildEntry(entry, owned, tenantId))
            .OfType<AuditLogEntry>()
            .ToList();

        context.AddRange(auditEntries);
    }

    private AuditLogEntry? BuildEntry(EntityEntry entry, List<EntityEntry> owned, Guid tenantId)
    {
        var fields = FieldsOf(entry).Concat(OwnedFieldsOf(entry, owned)).ToList();
        string action;
        string? before = null;
        string? after = null;

        switch (entry.State)
        {
            case EntityState.Added:
                action = "Created";
                after = ToJson(fields.Select(f => (f.Name, f.After)));
                break;
            case EntityState.Deleted:
                action = "Deleted";
                before = ToJson(fields.Select(f => (f.Name, f.Before)));
                break;
            default:
                // Unchanged too: only an owned value may have changed.
                var changed = fields.Where(f => f.IsChanged).ToList();
                if (changed.Count == 0)
                    return null;

                action = IsSoftDelete(entry) ? "Deleted" : "Updated";
                before = ToJson(changed.Select(f => (f.Name, f.Before)));
                after = ToJson(changed.Select(f => (f.Name, f.After)));
                break;
        }

        var request = _httpContextAccessor.HttpContext?.Request;
        return new AuditLogEntry
        {
            TenantId = tenantId,
            ActorUserId = _currentUser.UserId,
            ActorEmail = _currentUser.Email,
            Action = action,
            ResourceType = entry.Entity.GetType().Name,
            ResourceId = entry.Property("Id").CurrentValue?.ToString() ?? string.Empty,
            Before = before,
            After = after,
            OccurredAtUtc = DateTime.UtcNow,
            IpAddress = request?.HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Truncate(request?.Headers.UserAgent.ToString(), AuditLogEntry.UserAgentMaxLength),
        };
    }

    private static bool IsSoftDelete(EntityEntry entry) =>
        entry.Entity is ISoftDeletable
        && entry.Property(nameof(ISoftDeletable.IsDeleted)) is { IsModified: true, CurrentValue: true };

    private static IEnumerable<Field> FieldsOf(EntityEntry entry) =>
        entry.Properties.Select(p => new Field(p.Metadata.Name, p.OriginalValue, p.CurrentValue, p.IsModified));

    // EF tracks an owned value such as Job.SalaryRange as entries of its own: a new value is Added and
    // the one it replaces Deleted, so the pair is merged into one before and after per property.
    private static IEnumerable<Field> OwnedFieldsOf(EntityEntry owner, List<EntityEntry> owned) =>
        owned
            .Where(entry => IsOwnedBy(entry, owner))
            .SelectMany(entry => entry.Properties
                .Where(p => !p.Metadata.IsKey())
                .Select(p => (
                    Name: $"{entry.Metadata.FindOwnership()!.PrincipalToDependent!.Name}.{p.Metadata.Name}",
                    Before: entry.State == EntityState.Added ? null : p.OriginalValue,
                    After: entry.State == EntityState.Deleted ? null : p.CurrentValue)))
            .GroupBy(value => value.Name)
            .Select(group =>
            {
                var before = group.Select(v => v.Before).FirstOrDefault(v => v is not null);
                var after = group.Select(v => v.After).FirstOrDefault(v => v is not null);
                return new Field(group.Key, before, after, !Equals(before, after));
            });

    private static bool IsOwnedBy(EntityEntry entry, EntityEntry owner) =>
        entry.Metadata.FindOwnership() is { } ownership
        && ownership.PrincipalEntityType == owner.Metadata
        && ownership.Properties.Select(p => entry.Property(p.Name).CurrentValue)
            .SequenceEqual(ownership.PrincipalKey.Properties.Select(p => owner.Property(p.Name).CurrentValue));

    private static string ToJson(IEnumerable<(string Name, object? Value)> values) =>
        JsonSerializer.Serialize(values.ToDictionary(v => v.Name, v => ToJsonValue(v.Value)));

    // Enums and anything else are written as text, so the JSON stays readable.
    private static object? ToJsonValue(object? value) => value switch
    {
        null or bool or int or long or double or decimal or DateTime or Guid => value,
        _ => value.ToString(),
    };

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= maxLength ? value : value[..maxLength];

    private sealed record Field(string Name, object? Before, object? After, bool IsChanged);
}
