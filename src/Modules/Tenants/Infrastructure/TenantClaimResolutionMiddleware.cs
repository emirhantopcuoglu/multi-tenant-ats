using System.Security.Claims;
using Ats.Modules.Tenants.Application;
using Microsoft.AspNetCore.Http;

namespace Ats.Modules.Tenants.Infrastructure;

// Resolves the current tenant from the authenticated user's `tenant_id` claim.
// Runs after authentication and only when path-based resolution has not already
// set a tenant. This is what gives authenticated /api/v1 requests their tenant
// scope: those paths carry no slug, so the path-based middleware cannot resolve them.
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
            // The slug is not carried in the JWT, so it stays empty here; tenant
            // isolation (interceptor + query filter) only depends on the TenantId.
            concrete.SetTenant(tenantId, string.Empty);
        }

        await _next(context);
    }
}
