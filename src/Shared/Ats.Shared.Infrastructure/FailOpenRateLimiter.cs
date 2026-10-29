using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;

namespace Ats.Shared.Infrastructure;

// If Redis is down, let requests through instead of failing or hanging them. Limits aren't
// enforced during the outage. Normal rejections pass through unchanged, Retry-After included.
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
