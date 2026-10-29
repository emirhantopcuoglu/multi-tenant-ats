using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;

namespace Ats.Modules.Tenants.Domain;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public string PreferredLanguage { get; set; } = SupportedLanguages.Default;

    // Deactivated users can't sign in or refresh. The row stays because interviews, audit fields
    // and activity logs reference it. Not LockoutEnd: that is only enforced by SignInManager, which
    // isn't used here.
    public DateTime? DeactivatedAtUtc { get; set; }

    public bool IsActive => DeactivatedAtUtc is null;
}