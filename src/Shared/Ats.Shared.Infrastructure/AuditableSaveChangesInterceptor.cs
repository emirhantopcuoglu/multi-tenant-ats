using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ats.Shared.Infrastructure;

// Stamps audit fields and turns hard deletes into soft deletes on save. Kept
// separate from TenantSaveChangesInterceptor: tenant assignment and auditing are
// independent concerns, and either can be registered without the other.
public sealed class AuditableSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;

    public AuditableSaveChangesInterceptor(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
            return;

        var now = DateTime.UtcNow;
        var userId = _currentUser.UserId;

        // Convert deletes to soft deletes first; the row then flows through the
        // audit pass below as an ordinary modification.
        foreach (var entry in context.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted)
                continue;

            entry.State = EntityState.Modified;
            entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = true;
            entry.Property(nameof(ISoftDeletable.DeletedAtUtc)).CurrentValue = now;
            entry.Property(nameof(ISoftDeletable.DeletedBy)).CurrentValue = userId;
        }

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                // Respect values the domain already set (e.g. a factory) and only
                // fill the gaps, mirroring how the tenant interceptor behaves.
                if (entry.Entity.CreatedAtUtc == default)
                    entry.Property(nameof(IAuditable.CreatedAtUtc)).CurrentValue = now;
                if (entry.Entity.CreatedBy is null)
                    entry.Property(nameof(IAuditable.CreatedBy)).CurrentValue = userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IAuditable.ModifiedAtUtc)).CurrentValue = now;
                entry.Property(nameof(IAuditable.ModifiedBy)).CurrentValue = userId;
            }
        }
    }
}
