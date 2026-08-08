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
    // Distributed rate limiting (Sprint 4.4). Counters live in Redis (via the shared multiplexer), so the
    // limits hold across every app instance rather than per-process. Three fixed-window limits:
    //   - per-IP   (named policy) on login/register/public apply — unauthenticated abuse vectors
    //   - per-tenant + per-user (global, chained) on every authenticated request
    // The native middleware's default rejection is 503; OnRejected corrects it to 429 + Retry-After.
    public static IHostApplicationBuilder AddRateLimiting(this IHostApplicationBuilder builder)
    {
        var rateLimitingOptions = builder.Configuration
            .GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();
        var rateLimitWindow = TimeSpan.FromSeconds(rateLimitingOptions.WindowSeconds);

        builder.Services.AddRateLimiter(options =>
        {
            options.OnRejected = async (context, cancellationToken) =>
            {
                // The Redis limiter reports retry-after in seconds under its own metadata name, not the
                // framework's MetadataName.RetryAfter (which the built-in in-memory limiters use).
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

            // Behind a reverse proxy (Sprint 8) the real client IP arrives in X-Forwarded-For, which requires
            // ForwardedHeaders middleware to populate RemoteIpAddress. In dev it is correct as-is.
            options.AddPolicy(RateLimitPolicies.PerIp, httpContext =>
                FailOpenRedisFixedWindow(
                    httpContext,
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    rateLimitingOptions.PerIpPermitLimit,
                    rateLimitWindow));

            // CreateChained runs both limiters in sequence, so an authenticated request must satisfy its
            // tenant's shared budget and its own per-user budget. Unauthenticated requests carry neither
            // claim and fall through to NoLimiter here, relying on the per-IP policy instead.
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

    // Builds a Redis-backed fixed-window partition wrapped in FailOpenRateLimiter, so a Redis outage lets
    // the request through instead of failing it. The limiter for each key is created once and then cached
    // by the partition, so resolving the logger per call here is cheap. The connection multiplexer is
    // resolved from DI rather than captured, since AddCaching already registers it as a singleton — the
    // same instance either way, but this keeps the method free of shared local state from Program.cs.
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
