using System.Security.Claims;
using Ats.Modules.CandidateAccounts.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

// Marker requirement attached to the CandidateOnly policy. It carries no data of its own; the check
// lives in the handler below, which needs DI (the DbContext) and therefore cannot be the requirement.
public sealed class CandidateSecurityStampRequirement : IAuthorizationRequirement;

// Rejects candidate tokens whose security_stamp claim no longer matches the account's current stamp.
// A JWT is stateless — once minted it stays valid until it expires — so this per-request comparison
// is what lets a password change revoke every previously issued token immediately. Attached to the
// CandidateOnly policy, it covers every candidate endpoint in every module without any of them
// opting in. Cost: one indexed primary-key projection per authenticated candidate request.
//
// Scoped (not singleton like InterviewerAuthorizationHandler) because it depends on the scoped
// DbContext.
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
        // Tokens minted before the stamp existed carry no claim and fail here: those sessions are
        // forced through one re-login after deployment, which is the safe default for an auth change.
        // The 'sub' claim surfaces as NameIdentifier through the default inbound claim mapping.
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId) ||
            !Guid.TryParse(context.User.FindFirstValue(CandidateClaims.SecurityStamp), out var tokenStamp))
        {
            return;
        }

        // Projection instead of loading the entity: authorization runs on every request, so it should
        // read exactly one column via the primary key and nothing more. Guid.Empty can never match a
        // real stamp (accounts are born with a random one), so the not-found case falls out naturally.
        var currentStamp = await _db.CandidateAccounts
            .Where(c => c.Id == accountId)
            .Select(c => c.SecurityStamp)
            .FirstOrDefaultAsync();

        if (currentStamp == tokenStamp)
            context.Succeed(requirement);
    }
}
