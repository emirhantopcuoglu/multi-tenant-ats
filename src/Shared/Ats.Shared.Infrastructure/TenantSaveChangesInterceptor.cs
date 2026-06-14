using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ats.Shared.Infrastructure;

public sealed class TenantSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentTenant _currentTenant;

    public TenantSaveChangesInterceptor(ICurrentTenant currentTenant)
    {
        _currentTenant = currentTenant;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AssignTenantId(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AssignTenantId(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void AssignTenantId(DbContext? context)
    {
        if (context is null || _currentTenant.TenantId is null)
            return;

        var tenantId = _currentTenant.TenantId.Value;

        foreach (var entry in context.ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                entry.Property(nameof(ITenantScoped.TenantId)).CurrentValue = tenantId;
            }
        }
    }
}
