using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ats.UnitTests.Shared;

public class RetentionCleanupJobTests
{
    private static readonly RetentionOptions Defaults = new();

    [Fact]
    public async Task Should_give_every_cleaner_the_cutoffs_from_the_settings()
    {
        var first = new RecordingCleaner();
        var second = new RecordingCleaner();
        var before = DateTime.UtcNow;

        await CreateJob(Defaults, first, second).CleanupAsync();

        var after = DateTime.UtcNow;
        var cutoffs = Assert.Single(first.Received);
        Assert.Equal(cutoffs, Assert.Single(second.Received));
        Assert.InRange(cutoffs.AuditLogBeforeUtc, before.AddMonths(-Defaults.AuditLogMonths), after.AddMonths(-Defaults.AuditLogMonths));
        Assert.InRange(cutoffs.ReadNotificationBeforeUtc, before.AddDays(-Defaults.ReadNotificationDays), after.AddDays(-Defaults.ReadNotificationDays));
        Assert.InRange(cutoffs.ExpiredTokenBeforeUtc, before.AddDays(-Defaults.ExpiredTokenDays), after.AddDays(-Defaults.ExpiredTokenDays));
    }

    [Fact]
    public async Task One_cleaner_failing_should_not_stop_the_others_but_should_fail_the_run()
    {
        var failing = new RecordingCleaner { Failure = new TimeoutException("Postgres is slow.") };
        var next = new RecordingCleaner();

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateJob(Defaults, failing, next).CleanupAsync());

        Assert.Single(next.Received);
    }

    // Zero would delete the whole audit history; a negative token period would end live sessions.
    [Theory]
    [InlineData(0, 90, 30)]
    [InlineData(24, 0, 30)]
    [InlineData(24, 90, 0)]
    public async Task Should_refuse_a_period_under_one_before_deleting_anything(
        int auditLogMonths, int readNotificationDays, int expiredTokenDays)
    {
        var cleaner = new RecordingCleaner();
        var retention = new RetentionOptions
        {
            AuditLogMonths = auditLogMonths,
            ReadNotificationDays = readNotificationDays,
            ExpiredTokenDays = expiredTokenDays
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateJob(retention, cleaner).CleanupAsync());

        Assert.Empty(cleaner.Received);
    }

    private static RetentionCleanupJob CreateJob(RetentionOptions retention, params IRetentionCleaner[] cleaners) =>
        new(cleaners, Options.Create(retention), NullLogger<RetentionCleanupJob>.Instance);

    private sealed class RecordingCleaner : IRetentionCleaner
    {
        public List<RetentionCutoffs> Received { get; } = [];

        public Exception? Failure { get; init; }

        public Task<int> CleanAsync(RetentionCutoffs cutoffs, CancellationToken cancellationToken = default)
        {
            Received.Add(cutoffs);
            return Failure is null ? Task.FromResult(0) : Task.FromException<int>(Failure);
        }
    }
}
