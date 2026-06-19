using Ats.Modules.Tenants.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Tenants.Infrastructure;

// Resolves "which tenant does this request belong to?" from the leading path slug
// (e.g. /acmecorp/jobs) for public, pre-auth requests.
//
// The slug -> tenantId mapping is read on every public request but is effectively immutable
// (a tenant's slug is set at creation and never changes, and tenants are not deleted), so it is
// an ideal cache target. It is cached in Redis (IDistributedCache) — shared across all app
// instances, unlike an in-memory cache — to avoid hitting PostgreSQL on every request.
//
// The cache is best-effort / fail-open: this runs on every public request, so a Redis outage must
// degrade to a database read, never fail the request. Cache errors are logged (never silently
// swallowed) and the lookup falls back to the source of truth — the same stance as the activity
// log's best-effort write.
public sealed class TenantResolutionMiddleware
{
    // The mapping never changes, so the TTL is only a memory bound + safety net, not a correctness
    // mechanism. A newly created tenant is picked up on its first (cache-miss) request regardless.
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(1);

    private readonly RequestDelegate _next;
    private readonly IDistributedCache _cache;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        IDistributedCache cache,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, TenantsDbContext db)
    {
        var slug = ExtractSlug(context.Request.Path);

        if (slug is not null && tenantContext is TenantContext concrete)
        {
            var tenantId = await ResolveTenantIdAsync(slug, db, context.RequestAborted);
            if (tenantId is not null)
                concrete.SetTenant(tenantId.Value, slug);
        }

        await _next(context);
    }

    private async Task<Guid?> ResolveTenantIdAsync(string slug, TenantsDbContext db, CancellationToken cancellationToken)
    {
        var cached = await TryGetCachedTenantIdAsync(slug, cancellationToken);
        if (cached is not null)
            return cached;

        var tenantId = await db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Slug == slug)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        // Only positive lookups are cached. Skipping misses means a tenant registered just after a
        // failed lookup resolves immediately — there is no negative-cache window to go stale.
        if (tenantId is not null)
            await CacheTenantIdAsync(slug, tenantId.Value, cancellationToken);

        return tenantId;
    }

    private async Task<Guid?> TryGetCachedTenantIdAsync(string slug, CancellationToken cancellationToken)
    {
        try
        {
            var value = await _cache.GetStringAsync(CacheKey(slug), cancellationToken);
            return Guid.TryParse(value, out var id) ? id : null;
        }
        catch (Exception ex)
        {
            // Fail open: a cache read failure must not break tenant resolution.
            _logger.LogWarning(ex, "Tenant cache read failed for slug {Slug}; falling back to database.", slug);
            return null;
        }
    }

    private async Task CacheTenantIdAsync(string slug, Guid tenantId, CancellationToken cancellationToken)
    {
        try
        {
            await _cache.SetStringAsync(
                CacheKey(slug),
                tenantId.ToString(),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheLifetime },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tenant cache write failed for slug {Slug}; continuing without caching.", slug);
        }
    }

    // Namespaced so cache keys from other features cannot collide with tenant lookups.
    private static string CacheKey(string slug) => $"tenant:slug:{slug}";

    private static string? ExtractSlug(PathString path)
    {
        var segments = path.Value?.Trim('/').Split('/');
        if (segments is null || segments.Length == 0 || string.IsNullOrEmpty(segments[0]))
            return null;

        return segments[0].ToLowerInvariant();
    }
}
