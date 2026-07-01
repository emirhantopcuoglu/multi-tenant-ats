using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Shared.Kernel;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

// Mints the candidate access token. Structurally the same as the company TokenService, with two
// deliberate differences that keep the two identities apart: it stamps token_type=candidate, and it
// adds NO tenant_id and NO role claims. That combination is exactly what the CandidateOnly policy keys
// on, and why a candidate token cannot satisfy the role-gated company endpoints.
public sealed class CandidateTokenService : ICandidateTokenService
{
    private readonly CandidateJwtOptions _options;

    public CandidateTokenService(IOptions<CandidateJwtOptions> options)
    {
        _options = options.Value;
    }

    public string GenerateAccessToken(Guid candidateAccountId, string email)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, candidateAccountId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(TokenTypes.ClaimName, TokenTypes.Candidate)
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
}
