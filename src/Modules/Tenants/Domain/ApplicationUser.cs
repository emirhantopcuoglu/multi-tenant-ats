using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;

namespace Ats.Modules.Tenants.Domain;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // Which language this user is written to in — captured from the UI at registration or invitation
    // acceptance, and rewritten whenever they switch the toggle while signed in. Two-letter code;
    // membership of the supported set is enforced at the application boundary, not here, matching
    // how CandidateAccount handles the same field.
    public string PreferredLanguage { get; set; } = SupportedLanguages.Default;

    // When set, this person no longer works here: they cannot sign in and cannot refresh. The row is
    // deliberately kept rather than deleted — Interview.InterviewerUserIds, the audit columns
    // (CreatedBy/ModifiedBy) and the Mongo activity log all reference this id, so deleting it would
    // erase who ran last quarter's interviews rather than just revoking their access.
    //
    // Not Identity's own LockoutEnd: that models a temporary brute-force lock, and it is enforced by
    // SignInManager, which this codebase does not use (AuthService calls CheckPasswordAsync directly).
    // Reusing it would mean a flag that reads "locked out" but blocks nothing.
    public DateTime? DeactivatedAtUtc { get; set; }

    public bool IsActive => DeactivatedAtUtc is null;
}