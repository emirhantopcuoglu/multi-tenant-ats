using Ats.Shared.Kernel;

namespace Ats.Modules.Tenants.Application;

public sealed record AuthResult(string AccessToken, string RefreshToken);

public interface IAuthService
{
    Task<Result<AuthResult>> RegisterAsync(string companyName, string slug, string email, string password, string firstName, string lastName);
    Task<Result<AuthResult>> LoginAsync(string email, string password);
    Task<Result<AuthResult>> RefreshAsync(string refreshToken);
    Task<Result> LogoutAsync(string refreshToken);
}
