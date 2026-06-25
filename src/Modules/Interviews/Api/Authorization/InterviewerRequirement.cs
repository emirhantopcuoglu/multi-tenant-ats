using Microsoft.AspNetCore.Authorization;

namespace Ats.Modules.Interviews.Api.Authorization;

// Signals that the current user must appear in the interview's InterviewerUserIds list.
// Satisfied by InterviewerAuthorizationHandler when the resource is an InterviewDetailDto.
public sealed class InterviewerRequirement : IAuthorizationRequirement { }
