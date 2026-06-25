using System.Security.Claims;
using Ats.Modules.Interviews.Application.Interviews;
using Microsoft.AspNetCore.Authorization;

namespace Ats.Modules.Interviews.Api.Authorization;

// Grants access when the authenticated user's id appears in the interview's InterviewerUserIds.
// Works as a second gate after the role-based CanManageInterviews policy: role says "you have the
// right job title", this says "you were actually assigned to this specific interview".
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
