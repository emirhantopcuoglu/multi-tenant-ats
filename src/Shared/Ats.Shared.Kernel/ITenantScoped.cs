namespace Ats.Shared.Kernel;

public interface ITenantScoped
{
    Guid TenantId { get; }
}
