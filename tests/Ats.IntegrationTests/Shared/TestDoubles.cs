using Ats.Shared.Kernel;

namespace Ats.IntegrationTests.Shared;

internal sealed class FixedTenant : ICurrentTenant
{
    public FixedTenant(Guid? tenantId) => TenantId = tenantId;
    public Guid? TenantId { get; }
}

internal sealed class NullCurrentUser : ICurrentUser
{
    public Guid? UserId => null;
    public string? Email => null;
}
