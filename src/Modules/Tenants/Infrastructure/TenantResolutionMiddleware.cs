using Ats.Modules.Tenants.Application;
using Microsoft.AspNetCore.Http;

namespace Ats.Modules.Tenants.Infrastructure;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var slug = ExtractSlug(context.Request.Path);

        if (slug is not null && tenantContext is TenantContext concrete)
        {
            concrete.SetTenant(Guid.Empty, slug);
        }

        await _next(context);
    }

    private static string? ExtractSlug(PathString path)
    {
        var segments = path.Value?.Trim('/').Split('/');
        if (segments is null || segments.Length == 0 || string.IsNullOrEmpty(segments[0]))
            return null;

        return segments[0].ToLowerInvariant();
    }
}