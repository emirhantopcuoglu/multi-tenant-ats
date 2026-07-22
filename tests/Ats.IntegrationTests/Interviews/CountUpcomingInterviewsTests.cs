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

[Collection("Integration")]
public sealed class GetInterviewsForApplicationTests
{
    private readonly PostgresContainerFixture _fixture;

    public GetInterviewsForApplicationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_return_the_applications_interviews_in_schedule_order_for_the_given_tenant()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var applicationId = Guid.NewGuid();
        var later = DateTime.UtcNow.AddDays(5);
        var sooner = DateTime.UtcNow.AddDays(2);

        await using (var db = NewDb(tenant))
        {
            db.Interviews.Add(NewInterview(applicationId, later));
            db.Interviews.Add(NewInterview(applicationId, sooner));
            // A different application in the same tenant must not leak into the result.
            db.Interviews.Add(NewInterview(Guid.NewGuid(), sooner));
            await db.SaveChangesAsync();
        }

        await using var readDb = NewDb(tenant);
        var directory = new InterviewDirectory(readDb);

        var result = await directory.GetForApplicationAsync(tenant.TenantId!.Value, applicationId);

        Assert.Equal(2, result.Count);
        // Tolerance: PostgreSQL's timestamp column has microsecond precision, .NET's DateTime has
        // tick (100ns) precision — a round trip through the DB loses the last digit or two.
        Assert.Equal(sooner, result[0].ScheduledAtUtc, TimeSpan.FromMilliseconds(1));
        Assert.Equal(later, result[1].ScheduledAtUtc, TimeSpan.FromMilliseconds(1));
        Assert.All(result, i => Assert.Equal("Technical", i.Type));
    }

    [Fact]
    public async Task should_return_empty_when_the_tenant_does_not_own_the_application()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var applicationId = Guid.NewGuid();
        await using (var db = NewDb(tenant))
        {
            db.Interviews.Add(NewInterview(applicationId, DateTime.UtcNow.AddDays(1)));
            await db.SaveChangesAsync();
        }

        await using var readDb = NewDb(tenant);
        var directory = new InterviewDirectory(readDb);

        var result = await directory.GetForApplicationAsync(Guid.NewGuid(), applicationId);

        Assert.Empty(result);
    }

    private static Interview NewInterview(Guid applicationId, DateTime scheduledAtUtc) =>
        Interview.Schedule(
            applicationId, type: InterviewType.Technical, scheduledAtUtc: scheduledAtUtc,
            durationMinutes: 45, location: "Google Meet", interviewerUserIds: new[] { Guid.NewGuid() });

    private InterviewsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildInterviewsOptions(_fixture.ConnectionString, tenant), tenant);
}
