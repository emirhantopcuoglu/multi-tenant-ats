using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Shared.Kernel;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

// Like the company TokenService, but with token_type=candidate and no tenant or role claims,
// so a candidate token can't pass the role-gated company endpoints.
public sealed class CandidateTokenService : ICandidateTokenService
{
    private readonly CandidateJwtOptions _options;

    public CandidateTokenService(IOptions<CandidateJwtOptions> options)
    {
        _options = options.Value;
    }

    public string GenerateAccessToken(Guid candidateAccountId, string email, Guid securityStamp)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, candidateAccountId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(TokenTypes.ClaimName, TokenTypes.Candidate),
            new(CandidateClaims.SecurityStamp, securityStamp.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }
}
