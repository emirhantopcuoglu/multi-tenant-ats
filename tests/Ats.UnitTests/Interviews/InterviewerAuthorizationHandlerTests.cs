using System.Security.Claims;
using Ats.Modules.Interviews.Api.Authorization;
using Ats.Modules.Interviews.Application.Interviews;
using Microsoft.AspNetCore.Authorization;

namespace Ats.UnitTests.Interviews;

public class InterviewerAuthorizationHandlerTests
{
    private static readonly Guid InterviewerId = Guid.NewGuid();
    private static readonly Guid OtherId = Guid.NewGuid();

    private static InterviewDetailDto MakeDto(params Guid[] interviewerIds) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Technical", DateTime.UtcNow.AddDays(1),
            60, "Zoom", "Scheduled", null, interviewerIds.ToList());

    private static ClaimsPrincipal MakeUser(Guid? userId) =>
        new(new ClaimsIdentity(
            userId.HasValue
                ? [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())]
                : [],
            "TestAuth"));

    private static async Task<AuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user, InterviewDetailDto dto)
    {
        var handler = new InterviewerAuthorizationHandler();
        var requirement = new InterviewerRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, dto);
        await handler.HandleAsync(context);

        return context.HasSucceeded
            ? AuthorizationResult.Success()
            : AuthorizationResult.Failed();
    }

    [Fact]
    public async Task Succeed_when_user_is_listed_as_interviewer()
    {
        var result = await AuthorizeAsync(MakeUser(InterviewerId), MakeDto(InterviewerId));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Not_succeed_when_user_is_not_listed()
    {
        var result = await AuthorizeAsync(MakeUser(OtherId), MakeDto(InterviewerId));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Not_succeed_when_user_has_no_name_identifier_claim()
    {
        var result = await AuthorizeAsync(MakeUser(null), MakeDto(InterviewerId));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Succeed_when_user_is_one_of_multiple_interviewers()
    {
        var result = await AuthorizeAsync(
            MakeUser(InterviewerId), MakeDto(OtherId, InterviewerId));

        Assert.True(result.Succeeded);
    }
}
