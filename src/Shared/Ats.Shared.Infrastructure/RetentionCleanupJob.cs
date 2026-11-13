using Ats.Shared.Kernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ats.Shared.Infrastructure;

// Deletes records kept past their use: old audit rows, read notifications, expired sessions and requests.
// Each module says what it keeps. Scheduled by Hangfire.
public sealed class RetentionCleanupJob
{
    private const int MinimumPeriod = 1;

    private readonly IReadOnlyList<IRetentionCleaner> _cleaners;
    private readonly RetentionOptions _retention;
    private readonly ILogger<RetentionCleanupJob> _logger;

    public RetentionCleanupJob(
        IEnumerable<IRetentionCleaner> cleaners, IOptions<RetentionOptions> retention, ILogger<RetentionCleanupJob> logger)
    {
        _cleaners = cleaners.ToList();
        _retention = retention.Value;
        _logger = logger;
    }

    // One cleaner failing doesn't stop the others.
    public async Task CleanupAsync(CancellationToken ct = default)
    {
        var cutoffs = BuildCutoffs(DateTime.UtcNow);

        var failedCount = 0;
        foreach (var cleaner in _cleaners)
        {
            try
            {
                var deletedCount = await cleaner.CleanAsync(cutoffs, ct);
                _logger.LogInformation("{Cleaner} removed {DeletedCount} record(s)", cleaner.GetType(), deletedCount);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failedCount++;
                _logger.LogError(exception, "Retention cleaner {Cleaner} failed", cleaner.GetType());
            }
        }

        // Fails the run so it shows on the dashboard.
        if (failedCount > 0)
            throw new InvalidOperationException($"{failedCount} retention cleaner(s) failed.");
    }

    // A zero would delete the whole audit history at once; a negative token period would end live sessions.
    private RetentionCutoffs BuildCutoffs(DateTime nowUtc)
    {
        if (_retention.AuditLogMonths < MinimumPeriod
            || _retention.ReadNotificationDays < MinimumPeriod
            || _retention.ExpiredTokenDays < MinimumPeriod)
        {
            throw new InvalidOperationException(
                $"Every {RetentionOptions.SectionName} period must be at least {MinimumPeriod}.");
        }

        return new RetentionCutoffs(
            AuditLogBeforeUtc: nowUtc.AddMonths(-_retention.AuditLogMonths),
            ReadNotificationBeforeUtc: nowUtc.AddDays(-_retention.ReadNotificationDays),
            ExpiredTokenBeforeUtc: nowUtc.AddDays(-_retention.ExpiredTokenDays));
    }
}
