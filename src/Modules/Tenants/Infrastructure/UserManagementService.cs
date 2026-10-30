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

        // Keep at least one active Admin, or nobody could manage the tenant.
        if (currentRole == Roles.Admin && !await AnotherActiveAdminExistsAsync(user.Id, ct))
            return Result.Failure(UserManagementErrors.LastAdmin);

        // One role per user, so replace rather than add.
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

        // Takes effect when the access token is refreshed. Sessions are kept; a role change
        // isn't a reason to sign someone out.
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

        // Company tokens have no security stamp, so refresh tokens must be revoked explicitly.
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
        // Self is allowed: a deactivated user can't sign in to call this anyway.
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

    // Target must be in the caller's tenant and, except for reactivation, not the caller.
    private async Task<Result<ApplicationUser>> ResolveTargetAsync(
        Guid userId, CancellationToken ct, bool allowSelf = false)
    {
        if (_currentTenant.TenantId is not { } tenantId)
            return Result.Failure<ApplicationUser>(UserManagementErrors.TenantNotResolved);

        if (!allowSelf && _currentUser.UserId == userId)
            return Result.Failure<ApplicationUser>(UserManagementErrors.CannotTargetSelf);

        // ApplicationUser isn't ITenantScoped, so the tenant filter is explicit.
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct);

        return user is null
            ? Result.Failure<ApplicationUser>(UserManagementErrors.NotFound)
            : Result.Success(user);
    }

    // Excludes the user being changed; a deactivated Admin can't sign in, so it doesn't count.
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
