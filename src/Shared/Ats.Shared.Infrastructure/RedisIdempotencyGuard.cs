using Ats.Shared.Kernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Ats.Shared.Infrastructure;

// Redis-backed implementation of IIdempotencyGuard (Sprint 5.5). It claims a key atomically with
// SET NX (set-if-not-exists) and a TTL, so two concurrent deliveries of the same message cannot both
// pass the check — only the delivery that wins the SET runs the operation. The shared
// ConnectionMultiplexer is reused (same instance as the cache and rate limiter), and the database
// handle is cheap to obtain and reuse.
public sealed class RedisIdempotencyGuard : IIdempotencyGuard
{
    // The value stored under the key is irrelevant — only its presence matters — but a readable marker
    // helps when inspecting Redis during debugging.
    private const string ClaimMarker = "processed";

    private readonly IDatabase _database;
    private readonly TimeSpan _retention;
    private readonly ILogger<RedisIdempotencyGuard> _logger;

    public RedisIdempotencyGuard(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<IdempotencyOptions> options,
        ILogger<RedisIdempotencyGuard> logger)
    {
        _database = connectionMultiplexer.GetDatabase();
        _retention = TimeSpan.FromHours(options.Value.RetentionHours);
        _logger = logger;
    }

    public async Task<bool> ProcessOnceAsync(string key, Func<Task> operation)
    {
        bool claimed;
        try
        {
            // Atomic claim: succeeds only if the key does not already exist. A false result means a
            // prior delivery already processed this message, so we skip.
            claimed = await _database.StringSetAsync(key, ClaimMarker, _retention, When.NotExists);
        }
        catch (Exception exception)
        {
            // Fail-open, consistent with the cache and rate limiter: if Redis is unreachable we cannot
            // dedup, but dropping the side effect (e.g. the email) is worse than risking a duplicate, so
            // we run the operation without the guard.
            _logger.LogWarning(
                exception, "Idempotency store unavailable; processing without dedup for key {Key}", key);
            await operation();
            return true;
        }

        if (!claimed)
            return false;

        try
        {
            await operation();
            return true;
        }
        catch
        {
            // The operation failed, so release the claim to let MassTransit's retry (and, after retries
            // are exhausted, an error-queue replay) attempt it again. Best-effort: if the release itself
            // fails the marker simply expires after the retention window.
            try
            {
                await _database.KeyDeleteAsync(key);
            }
            catch (Exception releaseException)
            {
                _logger.LogWarning(
                    releaseException, "Failed to release idempotency claim for key {Key}", key);
            }

            throw;
        }
    }
}
