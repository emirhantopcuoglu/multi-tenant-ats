using Ats.Modules.Tenants.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Tenants.Infrastructure;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, TenantsDbContext db)
    {
        var slug = ExtractSlug(context.Request.Path);

        if (slug is not null && tenantContext is TenantContext concrete)
        {
            var tenant = await db.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Slug == slug);

            if (tenant is not null)
                concrete.SetTenant(tenant.Id, tenant.Slug);
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
