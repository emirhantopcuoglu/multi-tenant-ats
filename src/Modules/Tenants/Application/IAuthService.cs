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

// A member of the caller's tenant, for the interviewer picker (and the future users/settings screen).
public sealed record TenantUserDto(Guid Id, string FirstName, string LastName, string Email, string Role);

public interface IAuthService
{
    Task<Result<AuthResult>> RegisterAsync(string companyName, string slug, string email, string password, string firstName, string lastName);
    Task<Result<AuthResult>> LoginAsync(string email, string password);
    Task<Result<AuthResult>> RefreshAsync(string refreshToken);
    Task<Result> LogoutAsync(string refreshToken);
    Task<Result<CurrentUserDto>> GetCurrentUserAsync(Guid userId);
    /// <summary>Lists the members of the caller's tenant, ordered by name.</summary>
    Task<IReadOnlyList<TenantUserDto>> ListTenantUsersAsync();
}
