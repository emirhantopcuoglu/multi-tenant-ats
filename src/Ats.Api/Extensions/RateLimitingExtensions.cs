using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using RedisRateLimiting;
using StackExchange.Redis;

namespace Ats.Api.Extensions;

public static class RateLimitingExtensions
{
    // Counters in Redis, so limits hold across instances. Per-IP on anonymous endpoints, per-tenant and
    // per-user on authenticated ones. Rejections are 429 with Retry-After instead of the default 503.
    public static IHostApplicationBuilder AddRateLimiting(this IHostApplicationBuilder builder)
    {
        var rateLimitingOptions = builder.Configuration
            .GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();
        var rateLimitWindow = TimeSpan.FromSeconds(rateLimitingOptions.WindowSeconds);

        builder.Services.AddRateLimiter(options =>
        {
            options.OnRejected = async (context, cancellationToken) =>
            {
                // The Redis limiter uses its own metadata name for retry-after.
                if (context.Lease.TryGetMetadata(RateLimitMetadataName.RetryAfter, out var retryAfterSeconds))
                    context.HttpContext.Response.Headers.RetryAfter =
                        retryAfterSeconds.ToString(NumberFormatInfo.InvariantInfo);

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("RateLimiting")
                    .LogWarning("Rate limit exceeded for {Path}", context.HttpContext.Request.Path);

                await context.HttpContext.Response.WriteAsync(
                    "Too many requests. Please try again later.", cancellationToken);
            };

            options.AddPolicy(RateLimitPolicies.PerIp, httpContext =>
                FailOpenRedisFixedWindow(
                    httpContext,
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    rateLimitingOptions.PerIpPermitLimit,
                    rateLimitWindow));

            // Authenticated requests must fit both the tenant and the user budget. Anonymous ones get NoLimiter
            // here and rely on the per-IP policy.
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    var tenantId = httpContext.User.FindFirstValue("tenant_id");
                    return string.IsNullOrEmpty(tenantId)
                        ? RateLimitPartition.GetNoLimiter("unauthenticated")
                        : FailOpenRedisFixedWindow(
                            httpContext, $"tenant:{tenantId}", rateLimitingOptions.PerTenantPermitLimit, rateLimitWindow);
                }),
                PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                    return string.IsNullOrEmpty(userId)
                        ? RateLimitPartition.GetNoLimiter("unauthenticated")
                        : FailOpenRedisFixedWindow(
                            httpContext, $"user:{userId}", rateLimitingOptions.PerUserPermitLimit, rateLimitWindow);
                }));
        });

        return builder;
    }

    // Redis fixed window wrapped in FailOpenRateLimiter, so a Redis outage lets requests through.
    private static RateLimitPartition<string> FailOpenRedisFixedWindow(
        HttpContext httpContext, string key, int permitLimit, TimeSpan window)
    {
        var logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("RateLimiting");

        return RateLimitPartition.Get(key, partitionKey =>
            new FailOpenRateLimiter(
                new RedisFixedWindowRateLimiter<string>(partitionKey, new RedisFixedWindowRateLimiterOptions
                {
                    ConnectionMultiplexerFactory = () =>
                        httpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>(),
                    PermitLimit = permitLimit,
                    Window = window
                }),
                logger,
                partitionKey));
    }
}
