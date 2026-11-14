using System.Security.Claims;
using Ats.Modules.CandidateAccounts.Application;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

// Called when a candidate JWT is authenticated, so a password or email change (or a deleted account)
// ends existing sessions on every endpoint right away instead of when the JWT expires.
public sealed class CandidateSecurityStampValidator
{
    private readonly CandidateAccountsDbContext _db;

    public CandidateSecurityStampValidator(CandidateAccountsDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsCurrentAsync(ClaimsPrincipal candidate)
    {
        // 'sub' is mapped to NameIdentifier by the default inbound claim mapping.
        if (!Guid.TryParse(candidate.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId) ||
            !Guid.TryParse(candidate.FindFirstValue(CandidateClaims.SecurityStamp), out var tokenStamp))
        {
            return false;
        }

        // Runs on every request, so read one column. A missing account gives Guid.Empty, which never matches.
        var currentStamp = await _db.CandidateAccounts
            .Where(c => c.Id == accountId)
            .Select(c => c.SecurityStamp)
            .FirstOrDefaultAsync();

        return currentStamp == tokenStamp;
    }
}
