using Ats.IntegrationTests.Shared;
using Ats.Modules.Interviews.Application;
using Ats.Modules.Interviews.Domain;
using Ats.Modules.Interviews.Infrastructure;

namespace Ats.IntegrationTests.Interviews;

[Collection("Integration")]
public sealed class CountUpcomingInterviewsTests
{
    private readonly PostgresContainerFixture _fixture;

    public CountUpcomingInterviewsTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_count_only_scheduled_future_interviews_scoped_to_tenant()
    {
        // Arrange — target tenant: one upcoming (Scheduled), one Completed, one Cancelled. Another
        // tenant has an upcoming interview that must not leak. A fresh tenant id isolates each test.
        var scheduledAt = DateTime.UtcNow.AddDays(2);

        var tenant = new FixedTenant(Guid.NewGuid());
        await using (var db = NewDb(tenant))
        {
            db.Interviews.Add(NewInterview(scheduledAt));

            var completed = NewInterview(scheduledAt);
            completed.Complete();
            db.Interviews.Add(completed);

            var cancelled = NewInterview(scheduledAt);
            cancelled.Cancel();
            db.Interviews.Add(cancelled);

            await db.SaveChangesAsync();
        }

        var otherTenant = new FixedTenant(Guid.NewGuid());
        await using (var db = NewDb(otherTenant))
        {
            db.Interviews.Add(NewInterview(scheduledAt));
            await db.SaveChangesAsync();
        }

        await using var readDb = NewDb(tenant);
        var directory = new InterviewDirectory(readDb);

        // Act + Assert — only the single scheduled, future interview counts.
        Assert.Equal(1, await directory.CountUpcomingInterviewsAsync(DateTime.UtcNow));
        // Moving "now" past the scheduled time drops it from the upcoming count.
        Assert.Equal(0, await directory.CountUpcomingInterviewsAsync(scheduledAt.AddHours(1)));
    }

    private static Interview NewInterview(DateTime scheduledAtUtc) =>
        Interview.Schedule(
            applicationId: Guid.NewGuid(), type: InterviewType.Technical, scheduledAtUtc: scheduledAtUtc,
            durationMinutes: 60, location: "Google Meet", interviewerUserIds: new[] { Guid.NewGuid() });

    private InterviewsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildInterviewsOptions(_fixture.ConnectionString, tenant), tenant);
}
