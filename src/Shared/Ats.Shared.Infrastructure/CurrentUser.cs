using System.Security.Claims;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Http;

namespace Ats.Shared.Infrastructure;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // The JWT 'sub' claim is mapped to NameIdentifier by the default inbound claim
    // mapping, the same source JobsController reads the author from.
    public Guid? UserId =>
        Guid.TryParse(
            _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var id)
            ? id
            : null;
}
