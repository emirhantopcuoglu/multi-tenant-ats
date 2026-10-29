using Ats.Shared.Kernel;

namespace Ats.Modules.Tenants.Application;

public interface IUserManagementService
{
    /// <summary>Replaces a member's role. Refuses the caller and the last active Admin.</summary>
    Task<Result> ChangeRoleAsync(Guid userId, string role, CancellationToken ct = default);

    /// <summary>
    /// Blocks sign-in and refresh, and revokes refresh tokens right away. Refuses the caller and
    /// the last active Admin.
    /// </summary>
    Task<Result> DeactivateAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Restores a deactivated member. They sign in again with their existing password.</summary>
    Task<Result> ReactivateAsync(Guid userId, CancellationToken ct = default);
}
