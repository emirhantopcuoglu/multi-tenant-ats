using Ats.Modules.Tenants.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Tenants.Infrastructure;

// Resolves the tenant from the leading path slug (/acmecorp/jobs) for public requests.
// The slug never changes, so the lookup is cached in Redis. Cache errors fall back to the database.
public sealed class TenantResolutionMiddleware
{
    // The mapping never changes; the TTL only bounds memory.
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

        // Only hits are cached, so a newly registered tenant resolves immediately.
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

    private static string CacheKey(string slug) => $"tenant:slug:{slug}";

    private static string? ExtractSlug(PathString path)
    {
        var segments = path.Value?.Trim('/').Split('/');
        if (segments is null || segments.Length == 0 || string.IsNullOrEmpty(segments[0]))
            return null;

        return segments[0].ToLowerInvariant();
    }
}
