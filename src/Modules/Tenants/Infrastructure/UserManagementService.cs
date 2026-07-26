using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Tenants.Infrastructure;

public sealed class UserManagementService : IUserManagementService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TenantsDbContext _db;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(
        UserManager<ApplicationUser> userManager,
        TenantsDbContext db,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ILogger<UserManagementService> logger)
    {
        _userManager = userManager;
        _db = db;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result> ChangeRoleAsync(Guid userId, string role, CancellationToken ct = default)
    {
        if (!Roles.All.Contains(role))
            return Result.Failure(UserManagementErrors.InvalidRole);

        var resolved = await ResolveTargetAsync(userId, ct);
        if (resolved.IsFailure)
            return Result.Failure(resolved.Error);

        var user = resolved.Value;

        var currentRole = await GetRoleAsync(user);
        if (currentRole == role)
            return Result.Failure(UserManagementErrors.AlreadyInThatState);

        // Demoting the last Admin would leave the tenant with nobody who can manage users, invite
        // anyone, or edit the company profile — an unrecoverable state from inside the product.
        if (currentRole == Roles.Admin && !await AnotherActiveAdminExistsAsync(user.Id, ct))
            return Result.Failure(UserManagementErrors.LastAdmin);

        // One role per user is the established model (registration assigns Admin, invitations assign a
        // single role), so this replaces rather than adds.
        var existingRoles = await _userManager.GetRolesAsync(user);
        if (existingRoles.Count > 0)
        {
            var removed = await _userManager.RemoveFromRolesAsync(user, existingRoles);
            if (!removed.Succeeded)
                return Result.Failure(UserManagementErrors.InvalidRole);
        }

        var added = await _userManager.AddToRoleAsync(user, role);
        if (!added.Succeeded)
            return Result.Failure(UserManagementErrors.InvalidRole);

        // Their role lives in the access token, so the change only takes effect once that token is
        // replaced — up to AccessTokenMinutes away. Not revoking refresh tokens here is deliberate:
        // a role change is not a revocation of trust, and signing someone out mid-task to apply a
        // promotion would be worse than the short delay. Deactivation below is the opposite case.
        _logger.LogInformation(
            "Role of user {UserId} changed from {PreviousRole} to {NewRole}",
            user.Id, currentRole ?? "(none)", role);

        return Result.Success();
    }

    public async Task<Result> DeactivateAsync(Guid userId, CancellationToken ct = default)
    {
        var resolved = await ResolveTargetAsync(userId, ct);
        if (resolved.IsFailure)
            return Result.Failure(resolved.Error);

        var user = resolved.Value;
        if (!user.IsActive)
            return Result.Failure(UserManagementErrors.AlreadyInThatState);

        if (await GetRoleAsync(user) == Roles.Admin && !await AnotherActiveAdminExistsAsync(user.Id, ct))
            return Result.Failure(UserManagementErrors.LastAdmin);

        user.DeactivatedAtUtc = DateTime.UtcNow;

        // The reason this is not just a flag. Company tokens carry no security stamp and nothing
        // validates one per request, so a deactivated user's refresh token would otherwise keep
        // minting access tokens for its full RefreshTokenDays — the flag would block the login screen
        // while the person who already has a session carries on working. Same lesson as the password
        // reset in AuthService.ResetPasswordAsync.
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
            .ToListAsync(ct);

        foreach (var refreshToken in activeTokens)
            refreshToken.Revoke();

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "User {UserId} deactivated; revoked {RevokedCount} refresh token(s)",
            user.Id, activeTokens.Count);

        return Result.Success();
    }

    public async Task<Result> ReactivateAsync(Guid userId, CancellationToken ct = default)
    {
        // No self-check here: reactivating yourself is impossible anyway (a deactivated Admin cannot
        // authenticate to call this), so there is no footgun to guard against.
        var resolved = await ResolveTargetAsync(userId, ct, allowSelf: true);
        if (resolved.IsFailure)
            return Result.Failure(resolved.Error);

        var user = resolved.Value;
        if (user.IsActive)
            return Result.Failure(UserManagementErrors.AlreadyInThatState);

        user.DeactivatedAtUtc = null;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} reactivated", user.Id);
        return Result.Success();
    }

    // Loads the target and enforces the two rules every operation shares: it must be someone in the
    // caller's own tenant, and (except for reactivation) it must not be the caller.
    private async Task<Result<ApplicationUser>> ResolveTargetAsync(
        Guid userId, CancellationToken ct, bool allowSelf = false)
    {
        if (_currentTenant.TenantId is not { } tenantId)
            return Result.Failure<ApplicationUser>(UserManagementErrors.TenantNotResolved);

        if (!allowSelf && _currentUser.UserId == userId)
            return Result.Failure<ApplicationUser>(UserManagementErrors.CannotTargetSelf);

        // ApplicationUser is not ITenantScoped, so the tenant match is explicit rather than coming
        // from a global query filter. Without it an Admin could act on any user id on the platform.
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct);

        return user is null
            ? Result.Failure<ApplicationUser>(UserManagementErrors.NotFound)
            : Result.Success(user);
    }

    // "Another" and "active" both matter: the user being changed is excluded (they are the one losing
    // the role), and a deactivated Admin cannot sign in, so leaving one behind would lock the tenant
    // out just as effectively as having none.
    private async Task<bool> AnotherActiveAdminExistsAsync(Guid excludedUserId, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        return await (
            from user in _db.Users
            join userRole in _db.UserRoles on user.Id equals userRole.UserId
            join role in _db.Roles on userRole.RoleId equals role.Id
            where user.TenantId == tenantId
                  && user.Id != excludedUserId
                  && user.DeactivatedAtUtc == null
                  && role.Name == Roles.Admin
            select user.Id)
            .AnyAsync(ct);
    }

    private async Task<string?> GetRoleAsync(ApplicationUser user) =>
        (await _userManager.GetRolesAsync(user)).FirstOrDefault();
}
