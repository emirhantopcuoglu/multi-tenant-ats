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

    public Task<CandidateAuthResult> IssueAsync(CandidateAccount account) =>
        SaveAsync(account, (hash, expiresAtUtc) =>
            CandidateRefreshToken.Issue(account.Id, hash, account.SecurityStamp, expiresAtUtc));

    public Task<CandidateAuthResult> RotateAsync(CandidateRefreshToken spent, CandidateAccount account) =>
        SaveAsync(account, (hash, expiresAtUtc) => spent.Rotate(hash, account.SecurityStamp, expiresAtUtc));

    // One UPDATE instead of tracked entities: it skips the xmin check, so a refresh rotating one of
    // these tokens at the same moment can't fail the revocation.
    public Task<int> RevokeFamilyAsync(Guid familyId)
    {
        var now = DateTime.UtcNow;
        return _db.CandidateRefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now));
    }

    public Task<CandidateRefreshToken?> FindAsync(string refreshToken)
    {
        var hash = Hash(refreshToken);
        return _db.CandidateRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
    }

    private async Task<CandidateAuthResult> SaveAsync(
        CandidateAccount account, Func<string, DateTime, CandidateRefreshToken> createRefreshToken)
    {
        var accessToken = _tokenService.GenerateAccessToken(
            account.Id, account.Email, account.SecurityStamp);

        var refreshToken = _tokenService.GenerateRefreshToken();
        var expiresAtUtc = DateTime.UtcNow.AddDays(_options.RefreshTokenDays);
        _db.CandidateRefreshTokens.Add(createRefreshToken(Hash(refreshToken), expiresAtUtc));

        // Also saves what the caller staged (a rotation's revocation, a profile change), so the old
        // and new state commit together.
        await _db.SaveChangesAsync();

        return new CandidateAuthResult(accessToken, refreshToken, expiresAtUtc);
    }

    // No salt needed: the input is 64 random bytes, not a password.
    private static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }
}
