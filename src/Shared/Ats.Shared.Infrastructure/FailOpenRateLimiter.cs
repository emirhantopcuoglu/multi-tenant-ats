using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;

namespace Ats.Shared.Infrastructure;

// Decorates a rate limiter (here, a Redis-backed one) so that a backing-store failure does not take
// the request down with it. The rate limiter is a protective helper; if Redis is unreachable the
// right behavior for this project is to fail open — let the request through — exactly as the
// distributed cache does (Sprint 4.3): "a helper system's failure must not bring down the main
// operation." Without this, a Redis outage would turn rate limiting into a hard dependency that
// fails (and hangs, on the Redis command timeout) every request.
//
// The trade-off is explicit: while the backing store is down, limits are not enforced. For this
// project that is the accepted cost of keeping the API available. A normal rejection (limit reached
// while Redis is healthy) is passed straight through, so its lease — including Retry-After metadata —
// is preserved; only an actual store failure is swallowed.
public sealed class FailOpenRateLimiter : RateLimiter
{
    private static readonly RateLimitLease GrantedLease = new FailOpenLease();

    private readonly RateLimiter _inner;
    private readonly ILogger _logger;
    private readonly string _partitionKey;

    public FailOpenRateLimiter(RateLimiter inner, ILogger logger, string partitionKey)
    {
        _inner = inner;
        _logger = logger;
        _partitionKey = partitionKey;
    }

    public override TimeSpan? IdleDuration => _inner.IdleDuration;

    public override RateLimiterStatistics? GetStatistics() => _inner.GetStatistics();

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        try
        {
            return _inner.AttemptAcquire(permitCount);
        }
        catch (Exception exception)
        {
            LogFailOpen(exception);
            return GrantedLease;
        }
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(
        int permitCount, CancellationToken cancellationToken)
    {
        try
        {
            return await _inner.AcquireAsync(permitCount, cancellationToken);
        }
        // A cancelled request is a real cancellation, not a store failure — let it propagate.
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailOpen(exception);
            return GrantedLease;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();
    }

    protected override ValueTask DisposeAsyncCore() => _inner.DisposeAsync();

    private void LogFailOpen(Exception exception) =>
        _logger.LogWarning(
            exception,
            "Rate limiter backing store unavailable; allowing request (fail-open) for partition {Partition}",
            _partitionKey);

    // A lease that always reports success and carries no metadata — returned when we fail open.
    private sealed class FailOpenLease : RateLimitLease
    {
        public override bool IsAcquired => true;

        public override IEnumerable<string> MetadataNames => Array.Empty<string>();

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }
    }
}
