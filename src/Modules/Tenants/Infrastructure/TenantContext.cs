using Ats.Modules.Tenants.Application;

namespace Ats.Modules.Tenants.Infrastructure;

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public string? TenantSlug { get; private set; }
    public bool IsResolved => TenantId.HasValue;

    public void SetTenant(Guid tenantId, string tenantSlug)
    {
        TenantId = tenantId;
        TenantSlug = tenantSlug;
    }
}