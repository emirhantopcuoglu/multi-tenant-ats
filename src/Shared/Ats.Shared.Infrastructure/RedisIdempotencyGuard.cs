using Ats.Shared.Kernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Ats.Shared.Infrastructure;

// Claims the key with SET NX and a TTL, so only one of two concurrent deliveries runs.
public sealed class RedisIdempotencyGuard : IIdempotencyGuard
{
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
            // False means a previous delivery already handled it.
            claimed = await _database.StringSetAsync(key, ClaimMarker, _retention, When.NotExists);
        }
        catch (Exception exception)
        {
            // Fail open: a possible duplicate is better than dropping the email.
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
            // Release the claim so a retry can run. If this fails too, the key expires.
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
