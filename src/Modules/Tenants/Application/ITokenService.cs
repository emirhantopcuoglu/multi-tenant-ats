using Ats.Modules.Tenants.Domain;

namespace Ats.Modules.Tenants.Application;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);
    string GenerateRefreshToken();
}