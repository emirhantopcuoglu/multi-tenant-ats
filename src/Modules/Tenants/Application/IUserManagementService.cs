using Ats.Shared.Kernel;

namespace Ats.Modules.Tenants.Application;

// Administering the people in a tenant, kept apart from IAuthService: that one answers "who is
// calling and are they who they say" for anyone, while this one is a small set of privileged
// operations an Admin performs on somebody else. Folding them together would put "change another
// person's role" on the same interface the anonymous login endpoint depends on.
public interface IUserManagementService
{
    /// <summary>
    /// Replaces a tenant member's single role. Refuses to demote the caller themselves, or the last
    /// active Admin — a tenant with no Admin can never be administered again.
    /// </summary>
    Task<Result> ChangeRoleAsync(Guid userId, string role, CancellationToken ct = default);

    /// <summary>
    /// Revokes a member's access without deleting them: they can no longer sign in or refresh, and
    /// their outstanding refresh tokens are revoked so the cutoff is immediate rather than up to
    /// <c>RefreshTokenDays</c> later. Refuses the caller themselves and the last active Admin.
    /// </summary>
    Task<Result> DeactivateAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Restores a deactivated member. They sign in again with their existing password.</summary>
    Task<Result> ReactivateAsync(Guid userId, CancellationToken ct = default);
}
