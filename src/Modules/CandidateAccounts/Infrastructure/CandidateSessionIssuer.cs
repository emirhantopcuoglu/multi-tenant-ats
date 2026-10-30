using System.Security.Cryptography;
using System.Text;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

public sealed class CandidateSessionIssuer : ICandidateSessionIssuer
{
    private readonly CandidateAccountsDbContext _db;
    private readonly ICandidateTokenService _tokenService;
    private readonly CandidateJwtOptions _options;

    public CandidateSessionIssuer(
        CandidateAccountsDbContext db,
        ICandidateTokenService tokenService,
        IOptions<CandidateJwtOptions> options)
    {
        _db = db;
        _tokenService = tokenService;
        _options = options.Value;
    }

    public async Task<CandidateAuthResult> IssueAsync(CandidateAccount account)
    {
        var accessToken = _tokenService.GenerateAccessToken(
            account.Id, account.Email, account.SecurityStamp);

        var refreshToken = _tokenService.GenerateRefreshToken();
        _db.CandidateRefreshTokens.Add(CandidateRefreshToken.Issue(
            account.Id,
            Hash(refreshToken),
            account.SecurityStamp,
            DateTime.UtcNow.AddDays(_options.RefreshTokenDays)));

        // Also saves a revocation the refresh path staged, so old and new tokens commit together.
        await _db.SaveChangesAsync();

        return new CandidateAuthResult(accessToken, refreshToken);
    }

    public Task<CandidateRefreshToken?> FindAsync(string refreshToken)
    {
        var hash = Hash(refreshToken);
        return _db.CandidateRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
    }

    // No salt needed: the input is 64 random bytes, not a password.
    private static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }
}
