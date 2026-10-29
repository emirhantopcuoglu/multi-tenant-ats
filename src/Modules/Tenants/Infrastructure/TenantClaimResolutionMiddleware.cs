using System.Security.Claims;
using Ats.Modules.Tenants.Application;
using Microsoft.AspNetCore.Http;

namespace Ats.Modules.Tenants.Infrastructure;

// Sets the tenant from the tenant_id claim for authenticated /api/v1 requests, which have no slug.
// Runs after authentication and only if the path-based middleware didn't resolve one.
public sealed class TenantClaimResolutionMiddleware
{
    private const string TenantIdClaim = "tenant_id";

    private readonly RequestDelegate _next;

    public TenantClaimResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (!tenantContext.IsResolved
            && context.User.Identity?.IsAuthenticated == true
            && tenantContext is TenantContext concrete
            && Guid.TryParse(context.User.FindFirstValue(TenantIdClaim), out var tenantId))
        {
            // The JWT has no slug; tenant isolation only needs the id.
            concrete.SetTenant(tenantId, string.Empty);
        }

        await _next(context);
    }
}
