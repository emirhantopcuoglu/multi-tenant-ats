using Ats.IntegrationTests.Shared;
using Ats.Modules.Interviews.Application;
using Ats.Modules.Interviews.Application.Interviews;
using Ats.Modules.Interviews.Domain;
using Ats.Modules.Interviews.Infrastructure;

namespace Ats.IntegrationTests.Interviews;

// Covers the lifecycle buckets on GET /interviews. Deliberately an integration test rather than a
// unit test: the AwaitingOutcome and Upcoming buckets compare against the interview's *end* time,
// which is start + duration computed from a column. That expression has to survive translation into
// SQL — if Npgsql cannot render it, EF silently falls back to evaluating the predicate in memory and
// the filter would still "pass" a unit test while dragging the whole table across the wire.
[Collection("Integration")]
public sealed class ListInterviewsFilterTests
{
    private readonly PostgresContainerFixture _fixture;

    public ListInterviewsFilterTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_separate_elapsed_interviews_from_upcoming_ones()
    {
        // Arrange — one interview still ahead, one whose slot has already ended. Both are stored as
        // Scheduled: the difference between them exists only against the clock, which is exactly the
        // distinction the status column could not express.
        var tenant = new FixedTenant(Guid.NewGuid());
        var upcoming = DateTime.UtcNow.AddDays(2);
        var elapsed = DateTime.UtcNow.AddHours(-3);

        await using (var db = NewDb(tenant))
        {
            db.Interviews.Add(NewInterview(upcoming));
            db.Interviews.Add(NewInterview(elapsed));
            await db.SaveChangesAsync();
        }

        // Act + Assert
        var awaiting = await ListAsync(tenant, InterviewListFilter.AwaitingOutcome);
        var ahead = await ListAsync(tenant, InterviewListFilter.Upcoming);

        Assert.Equal(elapsed, Assert.Single(awaiting.Items).ScheduledAtUtc, TimeSpan.FromSeconds(1));
        Assert.Equal(upcoming, Assert.Single(ahead.Items).ScheduledAtUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task should_flag_an_elapsed_interview_as_awaiting_an_outcome()
    {
        var tenant = new FixedTenant(Guid.NewGuid());

        await using (var db = NewDb(tenant))
        {
            db.Interviews.Add(NewInterview(DateTime.UtcNow.AddHours(-3)));
            db.Interviews.Add(NewInterview(DateTime.UtcNow.AddDays(2)));
            await db.SaveChangesAsync();
        }

        var all = await ListAsync(tenant, filter: null);

        Assert.Equal(2, all.Items.Count);
        // Both rows still read Scheduled; only the derived flag tells them apart.
        Assert.All(all.Items, item => Assert.Equal(nameof(InterviewStatus.Scheduled), item.Status));
        Assert.Single(all.Items, item => item.IsAwaitingOutcome);
    }

    [Fact]
    public async Task should_not_treat_an_interview_still_in_progress_as_awaiting_an_outcome()
    {
        // Started 10 minutes ago, runs for 60: it is underway, not unresolved. This is the case the
        // end-time arithmetic exists for — comparing against the start time alone would get it wrong.
        var tenant = new FixedTenant(Guid.NewGuid());

        await using (var db = NewDb(tenant))
        {
            db.Interviews.Add(NewInterview(DateTime.UtcNow.AddMinutes(-10)));
            await db.SaveChangesAsync();
        }

        var awaiting = await ListAsync(tenant, InterviewListFilter.AwaitingOutcome);
        var ahead = await ListAsync(tenant, InterviewListFilter.Upcoming);

        Assert.Empty(awaiting.Items);
        Assert.Single(ahead.Items);
    }

    [Fact]
    public async Task should_exclude_interviews_that_already_have_an_outcome()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var slot = DateTime.UtcNow.AddHours(-3);

        await using (var db = NewDb(tenant))
        {
            var completed = NewInterview(slot);
            completed.Complete(slot.AddMinutes(1));
            db.Interviews.Add(completed);

            var cancelled = NewInterview(slot);
            cancelled.Cancel(slot.AddDays(-1));
            db.Interviews.Add(cancelled);

            await db.SaveChangesAsync();
        }

        Assert.Empty((await ListAsync(tenant, InterviewListFilter.AwaitingOutcome)).Items);
        Assert.Single((await ListAsync(tenant, InterviewListFilter.Completed)).Items);
        Assert.Single((await ListAsync(tenant, InterviewListFilter.Cancelled)).Items);
    }

    private async Task<PagedResult<InterviewListItemDto>> ListAsync(
        FixedTenant tenant, InterviewListFilter? filter)
    {
        await using var db = NewDb(tenant);
        var handler = new ListInterviewsHandler(db, new FakeApplicationDirectory(null));
        var result = await handler.Handle(
            new ListInterviewsQuery(Filter: filter), CancellationToken.None);
        return result.Value;
    }

    // Booked a day before its slot, so a deliberately-past scheduled time still satisfies the
    // minimum-lead-time rule that applies at booking.
    private static Interview NewInterview(DateTime scheduledAtUtc) =>
        Interview.Schedule(
            applicationId: Guid.NewGuid(), type: InterviewType.Technical,
            scheduledAtUtc: scheduledAtUtc, durationMinutes: 60,
            interviewerUserIds: new[] { Guid.NewGuid() },
            nowUtc: scheduledAtUtc.AddDays(-1));

    private InterviewsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildInterviewsOptions(_fixture.ConnectionString, tenant), tenant);
}
