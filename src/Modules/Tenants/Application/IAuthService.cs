using Ats.Shared.Kernel;

namespace Ats.Modules.Tenants.Application;

public sealed record AuthResult(string AccessToken, string RefreshToken);

// Adds the display name and company name, which the JWT doesn't carry.
public sealed record CurrentUserDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    CurrentUserTenantDto Tenant);

public sealed record CurrentUserTenantDto(string CompanyName, string Slug);

// IsActive lets Settings show deactivated users so they can be reactivated.
public sealed record TenantUserDto(
    Guid Id, string FirstName, string LastName, string Email, string Role, bool IsActive);

public interface IAuthService
{
    /// <summary>
    /// Creates the tenant and its first Admin, then mails a confirmation link. Returns no tokens:
    /// login waits for the confirmed email.
    /// </summary>
    Task<Result> RegisterAsync(
        string companyName, string slug, string email, string password, string firstName, string lastName,
        string preferredLanguage);

    Task<Result<AuthResult>> LoginAsync(string email, string password);

    /// <summary>Confirms the email. Already-confirmed accounts also succeed.</summary>
    Task<Result> ConfirmEmailAsync(Guid userId, string token, CancellationToken ct = default);

    /// <summary>
    /// Mails a new confirmation link. Always succeeds, so it can't reveal registered addresses.
    /// </summary>
    Task<Result> ResendEmailConfirmationAsync(string email, CancellationToken ct = default);
    Task<Result<AuthResult>> RefreshAsync(string refreshToken);
    Task<Result> LogoutAsync(string refreshToken);

    /// <summary>
    /// Mails a reset link if the user exists. Always succeeds, so it can't reveal registered addresses.
    /// </summary>
    Task<Result> RequestPasswordResetAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Sets a new password from a mailed token and revokes the user's refresh tokens. Returns no tokens.
    /// </summary>
    Task<Result> ResetPasswordAsync(
        Guid userId, string token, string newPassword, CancellationToken ct = default);
    Task<Result<CurrentUserDto>> GetCurrentUserAsync(Guid userId);

    /// <summary>Sets the language emails are sent in.</summary>
    Task<Result> SetPreferredLanguageAsync(Guid userId, string language, CancellationToken ct = default);
    /// <summary>Lists the members of the caller's tenant, active users first.</summary>
    Task<IReadOnlyList<TenantUserDto>> ListTenantUsersAsync();
}
