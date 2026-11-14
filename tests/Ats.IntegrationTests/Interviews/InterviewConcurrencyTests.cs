using Ats.IntegrationTests.Shared;
using Ats.Modules.Interviews.Domain;
using Ats.Modules.Interviews.Infrastructure;
using Ats.Shared.Contracts.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ats.IntegrationTests.Interviews;

[Collection("Integration")]
public sealed class InterviewConcurrencyTests
{
    private const int Duration = 30;

    private readonly PostgresContainerFixture _fixture;

    public InterviewConcurrencyTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_refuse_a_reschedule_based_on_a_copy_read_before_a_concurrent_cancel()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var interviewId = await SeedAsync(tenant, DateTime.UtcNow.AddDays(2));

        await using var reschedulerDb = NewDb(tenant);
        await using var cancellerDb = NewDb(tenant);
        var reschedulerCopy = await reschedulerDb.Interviews.SingleAsync(i => i.Id == interviewId);
        var cancellerCopy = await cancellerDb.Interviews.SingleAsync(i => i.Id == interviewId);

        cancellerCopy.Cancel(InterviewCancellationReason.CandidateWithdrew, note: null, DateTime.UtcNow);
        await cancellerDb.SaveChangesAsync();

        reschedulerCopy.Reschedule(DateTime.UtcNow.AddDays(5), Duration, DateTime.UtcNow);
        var save = () => reschedulerDb.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(save);
        var stored = await LoadAsync(tenant, interviewId);
        Assert.Equal(InterviewStatus.Cancelled, stored.Status);
        Assert.Null(stored.DayBeforeReminderDueAtUtc);
    }

    // The sweep loads due interviews, then awaits the application lookup before saving. A reschedule
    // that lands in that gap must keep the reminder it owes for the new time.
    [Fact]
    public async Task should_keep_the_new_reminder_when_a_reschedule_lands_during_the_sweep()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var application = new ApplicationForScheduling(
            Guid.NewGuid(), IsActive: true, Guid.NewGuid(), "Staff Engineer",
            Guid.NewGuid(), Guid.NewGuid(), "raced@acme.test", "Race");
        var interviewId = await SeedAsync(tenant, DateTime.UtcNow.AddDays(2), application.Id, dayBeforeIsDue: true);
        var newStart = DateTime.UtcNow.AddDays(7);

        var directory = new InterleavingApplicationDirectory(
            new FakeApplicationDirectory(application),
            () => RescheduleAsync(tenant, interviewId, newStart));
        await using var sweepDb = NewDb(new FixedTenant(null));
        var job = new InterviewReminderJob(
            sweepDb, directory, new CapturingPublisher(), NullLogger<InterviewReminderJob>.Instance);

        var sweep = () => job.SendDueRemindersAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(sweep);
        var stored = await LoadAsync(tenant, interviewId);
        Assert.Equal(newStart.AddHours(-Interview.DayBeforeReminderLeadHours), stored.DayBeforeReminderDueAtUtc!.Value, TimeSpan.FromSeconds(1));
    }

    private async Task RescheduleAsync(FixedTenant tenant, Guid interviewId, DateTime newStart)
    {
        await using var db = NewDb(tenant);
        var interview = await db.Interviews.SingleAsync(i => i.Id == interviewId);
        interview.Reschedule(newStart, Duration, DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedAsync(
        FixedTenant tenant, DateTime scheduledAtUtc, Guid? applicationId = null, bool dayBeforeIsDue = false)
    {
        await using var db = NewDb(tenant);
        var interview = Interview.Schedule(
            applicationId ?? Guid.NewGuid(), InterviewType.Technical, scheduledAtUtc, Duration,
            [Guid.NewGuid()], DateTime.UtcNow);
        db.Interviews.Add(interview);
        await db.SaveChangesAsync();

        if (dayBeforeIsDue)
        {
            await db.Interviews.Where(i => i.Id == interview.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.DayBeforeReminderDueAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        }

        return interview.Id;
    }

    private async Task<Interview> LoadAsync(FixedTenant tenant, Guid interviewId)
    {
        await using var db = NewDb(tenant);
        return await db.Interviews.AsNoTracking().SingleAsync(i => i.Id == interviewId);
    }

    private InterviewsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildInterviewsOptions(_fixture.ConnectionString, tenant), tenant);

    // Runs another user's write in the middle of the sweep's batched lookup.
    private sealed class InterleavingApplicationDirectory : IApplicationDirectory
    {
        private readonly IApplicationDirectory _inner;
        private readonly Func<Task> _concurrentWrite;

        public InterleavingApplicationDirectory(IApplicationDirectory inner, Func<Task> concurrentWrite)
        {
            _inner = inner;
            _concurrentWrite = concurrentWrite;
        }

        public async Task<IReadOnlyDictionary<Guid, ApplicationForScheduling>> GetForSchedulingAsync(
            IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken = default)
        {
            await _concurrentWrite();
            return await _inner.GetForSchedulingAsync(applicationIds, cancellationToken);
        }

        public Task<ApplicationForScheduling?> GetForSchedulingAsync(
            Guid applicationId, CancellationToken cancellationToken = default) =>
            _inner.GetForSchedulingAsync(applicationId, cancellationToken);

        public Task<ApplicationForScheduling?> GetForSchedulingAsync(
            Guid tenantId, Guid applicationId, CancellationToken cancellationToken = default) =>
            _inner.GetForSchedulingAsync(tenantId, applicationId, cancellationToken);

        public Task<IReadOnlyDictionary<Guid, string>> GetCandidateNamesByApplicationAsync(
            IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken = default) =>
            _inner.GetCandidateNamesByApplicationAsync(applicationIds, cancellationToken);

        public Task<IReadOnlyList<Guid>> GetApplicationIdsForCandidateAsync(
            Guid candidateId, CancellationToken cancellationToken = default) =>
            _inner.GetApplicationIdsForCandidateAsync(candidateId, cancellationToken);

        public Task<IReadOnlyList<Guid>> GetApplicationIdsForCandidateAccountAsync(
            Guid candidateAccountId, CancellationToken cancellationToken = default) =>
            _inner.GetApplicationIdsForCandidateAccountAsync(candidateAccountId, cancellationToken);

        public Task<int> CountApplicationsSinceAsync(
            DateTime sinceUtc, CancellationToken cancellationToken = default) =>
            _inner.CountApplicationsSinceAsync(sinceUtc, cancellationToken);

        public Task<int> CountActiveCandidatesAsync(CancellationToken cancellationToken = default) =>
            _inner.CountActiveCandidatesAsync(cancellationToken);
    }
}
