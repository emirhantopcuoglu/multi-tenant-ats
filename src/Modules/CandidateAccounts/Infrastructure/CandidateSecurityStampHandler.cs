using System.Security.Claims;
using Ats.Modules.CandidateAccounts.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

public sealed class CandidateSecurityStampRequirement : IAuthorizationRequirement;

// Part of the CandidateOnly policy. Rejects tokens whose stamp no longer matches the account, so a
// password or email change ends existing sessions right away instead of when the JWT expires.
public sealed class CandidateSecurityStampHandler : AuthorizationHandler<CandidateSecurityStampRequirement>
{
    private readonly CandidateAccountsDbContext _db;

    public CandidateSecurityStampHandler(CandidateAccountsDbContext db)
    {
        _db = db;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, CandidateSecurityStampRequirement requirement)
    {
        // 'sub' is mapped to NameIdentifier by the default inbound claim mapping.
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId) ||
            !Guid.TryParse(context.User.FindFirstValue(CandidateClaims.SecurityStamp), out var tokenStamp))
        {
            return;
        }

        // Runs on every request, so read one column. A missing account gives Guid.Empty, which never matches.
        var currentStamp = await _db.CandidateAccounts
            .Where(c => c.Id == accountId)
            .Select(c => c.SecurityStamp)
            .FirstOrDefaultAsync();

        if (currentStamp == tokenStamp)
            context.Succeed(requirement);
    }
}
