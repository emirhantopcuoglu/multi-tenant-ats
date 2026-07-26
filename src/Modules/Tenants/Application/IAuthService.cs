using Ats.Shared.Kernel;

namespace Ats.Modules.Tenants.Application;

public sealed record AuthResult(string AccessToken, string RefreshToken);

// The authenticated user's profile for the topbar and role-based UI. The JWT already carries the id,
// email, role, and tenant_id, but not the display name or company name — this endpoint fills that gap.
public sealed record CurrentUserDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    CurrentUserTenantDto Tenant);

public sealed record CurrentUserTenantDto(string CompanyName, string Slug);

// A member of the caller's tenant, for the interviewer picker and the users/settings screen.
// IsActive is included so Settings can show a deactivated colleague (and offer to reactivate them)
// rather than having them vanish from the list with no way back.
public sealed record TenantUserDto(
    Guid Id, string FirstName, string LastName, string Email, string Role, bool IsActive);

public interface IAuthService
{
    /// <summary>
    /// Creates the tenant and its founding Admin, then mails a confirmation link. Deliberately returns
    /// no tokens: the session waits until the address is proven, because a company user who can sign in
    /// can already invite colleagues, publish jobs and receive applications.
    /// </summary>
    Task<Result> RegisterAsync(string companyName, string slug, string email, string password, string firstName, string lastName);

    Task<Result<AuthResult>> LoginAsync(string email, string password);

    /// <summary>
    /// Marks the address proven from a mailed token. Succeeds for an already-confirmed account so a
    /// second click of the same link never reports a working account as broken.
    /// </summary>
    Task<Result> ConfirmEmailAsync(Guid userId, string token, CancellationToken ct = default);

    /// <summary>
    /// Mails a fresh confirmation link. Anonymous by necessity — whoever needs it cannot sign in — so
    /// it reports success for unknown and already-confirmed addresses alike rather than becoming a
    /// directory of who works here.
    /// </summary>
    Task<Result> ResendEmailConfirmationAsync(string email, CancellationToken ct = default);
    Task<Result<AuthResult>> RefreshAsync(string refreshToken);
    Task<Result> LogoutAsync(string refreshToken);

    /// <summary>
    /// Mails a reset link if a user with this email exists. Reports success either way so the endpoint
    /// cannot be used to discover which addresses are registered.
    /// </summary>
    Task<Result> RequestPasswordResetAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Sets a new password from a mailed token and revokes the user's refresh tokens. Unlike the
    /// candidate side there is no per-request security-stamp check on company tokens, so that
    /// revocation is explicit here — without it a reset would leave a stolen refresh token valid for
    /// its full week. Returns no tokens: the user signs in with the password they just chose.
    /// </summary>
    Task<Result> ResetPasswordAsync(
        Guid userId, string token, string newPassword, CancellationToken ct = default);
    Task<Result<CurrentUserDto>> GetCurrentUserAsync(Guid userId);
    /// <summary>Lists the members of the caller's tenant, ordered by name.</summary>
    Task<IReadOnlyList<TenantUserDto>> ListTenantUsersAsync();
}
