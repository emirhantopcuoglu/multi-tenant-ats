namespace Ats.Shared.Kernel;

public interface ICurrentTenant
{
    Guid? TenantId { get; }
}
