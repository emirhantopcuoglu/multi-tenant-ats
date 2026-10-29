using System.Security.Claims;
using Ats.Modules.Interviews.Application.Interviews;
using Microsoft.AspNetCore.Authorization;

namespace Ats.Modules.Interviews.Api.Authorization;

// Succeeds when the current user is on the interview's panel. The role policy only checks the role.
public sealed class InterviewerAuthorizationHandler
    : AuthorizationHandler<InterviewerRequirement, InterviewDetailDto>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        InterviewerRequirement requirement,
        InterviewDetailDto interview)
    {
        var userIdStr = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (Guid.TryParse(userIdStr, out var userId) && interview.InterviewerUserIds.Contains(userId))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
