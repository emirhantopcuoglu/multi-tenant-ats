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

        // One SaveChanges for the whole scope: when the refresh path stages a revocation before
        // calling this, the spent row and its replacement commit together or not at all.
        await _db.SaveChangesAsync();

        // The raw token is returned here and never stored — only its hash was persisted above, so
        // this is the only moment it exists outside the client.
        return new CandidateAuthResult(accessToken, refreshToken);
    }

    public Task<CandidateRefreshToken?> FindAsync(string refreshToken)
    {
        var hash = Hash(refreshToken);
        return _db.CandidateRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
    }

    // Only the hash is stored, so a leaked table cannot be replayed as a set of live sessions. Plain
    // SHA-256 with no salt or work factor on purpose: the input is 64 bytes of CSPRNG output, not a
    // human-chosen password, so there is nothing for a dictionary or rainbow table to attack. Same
    // construction as the company AuthService's own token hash.
    private static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }
}
