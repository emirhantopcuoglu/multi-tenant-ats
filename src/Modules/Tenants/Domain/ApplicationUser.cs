using Microsoft.AspNetCore.Identity;

namespace Ats.Modules.Tenants.Domain;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}