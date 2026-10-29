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

    // 'sub' is mapped to NameIdentifier by the default inbound claim mapping.
    public Guid? UserId =>
        Guid.TryParse(
            _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var id)
            ? id
            : null;

    public string? Email =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);
}
