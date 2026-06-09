namespace Ats.Modules.Tenants.Application;

public sealed record AuthResult(string AccessToken, string RefreshToken);

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string companyName, string slug, string email, string password, string firstName, string lastName);
    Task<AuthResult> LoginAsync(string email, string password);
    Task<AuthResult> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
}