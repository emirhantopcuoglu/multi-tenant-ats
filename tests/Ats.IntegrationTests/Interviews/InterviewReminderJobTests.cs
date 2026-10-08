using Ats.IntegrationTests.Shared;
using Ats.Modules.Interviews.Application.Events;
using Ats.Modules.Interviews.Domain;
using Ats.Modules.Interviews.Infrastructure;
using Ats.Shared.Contracts.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ats.IntegrationTests.Interviews;

// The sweep against a real database, because the parts most likely to break are the ones an
// in-memory fake would not have: the cross-tenant read (a background job has no ambient tenant, so
// the global query filter matches nothing without an explicit bypass) and the fact that a settled
// reminder is actually persisted as settled.
//
// Every test drives the job twice where it matters — the whole point of the design is that a second
// run must not email anyone a second time.
[Collection("Integration")]
public sealed class InterviewReminderJobTests
{
    private const int Duration = 30;

    private readonly PostgresContainerFixture _fixture;

    public InterviewReminderJobTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_publish_the_day_before_reminder_once_it_falls_due()
    {
        // Arrange — an interview two days out whose day-before reminder has just come due. The
        // starting-soon one is still a day and a half away.
        var tenant = new FixedTenant(Guid.NewGuid());
        var application = SomeApplication();
        var interviewId = await SeedAsync(
            tenant, application.Id, DateTime.UtcNow.AddDays(2), dayBeforeIsDue: true);
        var publisher = new CapturingPublisher();

        // Act
        await RunSweepAsync(application, publisher);

        // Assert
        var reminder = Assert.IsType<InterviewReminderDueEvent>(Assert.Single(publisher.Published));
        Assert.Equal(interviewId, reminder.InterviewId);
        Assert.Equal(InterviewReminderKind.DayBefore, reminder.Kind);
        Assert.Equal(application.CandidateEmail, reminder.CandidateEmail);
        Assert.Equal(application.JobTitle, reminder.JobTitle);
        Assert.Equal(tenant.TenantId!.Value, reminder.TenantId);

        // The reminder is settled, and only that one: the starting-soon nudge is still owed.
        var stored = await LoadAsync(tenant, interviewId);
        Assert.Null(stored.DayBeforeReminderDueAtUtc);
        Assert.NotNull(stored.StartingSoonReminderDueAtUtc);
    }

    [Fact]
    public async Task should_not_publish_the_same_reminder_on_a_second_run()
    {
        // The property the whole "due instant, cleared when settled" design exists for. Without it
        // the sweep would re-send every reminder on every run for the life of the interview.
        var tenant = new FixedTenant(Guid.NewGuid());
        var application = SomeApplication();
        await SeedAsync(tenant, application.Id, DateTime.UtcNow.AddDays(2), dayBeforeIsDue: true);

        var first = new CapturingPublisher();
        await RunSweepAsync(application, first);

        var second = new CapturingPublisher();
        await RunSweepAsync(application, second);

        Assert.Single(first.Published);
        Assert.Empty(second.Published);
    }

    [Fact]
    public async Task should_send_only_the_starting_soon_reminder_when_both_fell_due()
    {
        // A sweep that has not run for a while — after a restart, say — finds both owed. Only the
        // later one is still true; "your interview is tomorrow" ten minutes before it starts reads
        // as a broken system. The day-before one is settled without being sent.
        var tenant = new FixedTenant(Guid.NewGuid());
        var application = SomeApplication();
        var interviewId = await SeedAsync(
            tenant, application.Id, DateTime.UtcNow.AddDays(2),
            dayBeforeIsDue: true, startingSoonIsDue: true);
        var publisher = new CapturingPublisher();

        await RunSweepAsync(application, publisher);

        var reminder = Assert.IsType<InterviewReminderDueEvent>(Assert.Single(publisher.Published));
        Assert.Equal(InterviewReminderKind.StartingSoon, reminder.Kind);

        var stored = await LoadAsync(tenant, interviewId);
        Assert.Null(stored.DayBeforeReminderDueAtUtc);
        Assert.Null(stored.StartingSoonReminderDueAtUtc);
    }

    [Fact]
    public async Task should_settle_without_sending_when_the_interview_was_cancelled()
    {
        // Cancel already clears both columns, so this asserts the belt as well as the braces: even
        // if a due instant survived, a non-pending interview must not reach the candidate.
        var tenant = new FixedTenant(Guid.NewGuid());
        var application = SomeApplication();
        var interviewId = await SeedAsync(
            tenant, application.Id, DateTime.UtcNow.AddDays(2), dayBeforeIsDue: true);

        await using (var db = NewDb(tenant))
        {
            var interview = await db.Interviews.SingleAsync(i => i.Id == interviewId);
            interview.Cancel(InterviewCancellationReason.PositionClosed, note: null, DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        var publisher = new CapturingPublisher();
        await RunSweepAsync(application, publisher);

        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task should_settle_without_sending_when_the_application_is_gone()
    {
        // The directory answers with nothing for this id. The reminder must still be cleared, or the
        // sweep would re-examine a dead interview on every run from now on.
        var tenant = new FixedTenant(Guid.NewGuid());
        var interviewId = await SeedAsync(
            tenant, Guid.NewGuid(), DateTime.UtcNow.AddDays(2), dayBeforeIsDue: true);
        var publisher = new CapturingPublisher();

        await RunSweepAsync(application: null, publisher);

        Assert.Empty(publisher.Published);
        Assert.Null((await LoadAsync(tenant, interviewId)).DayBeforeReminderDueAtUtc);
    }

    [Fact]
    public async Task should_not_touch_an_interview_whose_reminders_are_still_in_the_future()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var application = SomeApplication();
        var interviewId = await SeedAsync(tenant, application.Id, DateTime.UtcNow.AddDays(10));
        var publisher = new CapturingPublisher();

        await RunSweepAsync(application, publisher);

        Assert.Empty(publisher.Published);
        var stored = await LoadAsync(tenant, interviewId);
        Assert.NotNull(stored.DayBeforeReminderDueAtUtc);
        Assert.NotNull(stored.StartingSoonReminderDueAtUtc);
    }

    // The job resolves its own tenant scope from the interview rows, so it is deliberately run
    // through a context whose ambient tenant is a different one — if the cross-tenant bypass were
    // missing, every test above would find nothing and pass for the wrong reason.
    private async Task RunSweepAsync(
        ApplicationForScheduling? application, CapturingPublisher publisher)
    {
        await using var db = NewDb(new FixedTenant(Guid.NewGuid()));

        var job = new InterviewReminderJob(
            db,
            new FakeApplicationDirectory(application),
            publisher,
            NullLogger<InterviewReminderJob>.Instance);

        await job.SendDueRemindersAsync();
    }

    // The flags drag a due instant into the past without moving the interview itself, which is the
    // only way to observe a due reminder without waiting a day for one. Written straight to the
    // columns: the domain offers no "pretend this came due", and it should not — that is a test
    // concern, not a business operation.
    private async Task<Guid> SeedAsync(
        FixedTenant tenant, Guid applicationId, DateTime scheduledAtUtc,
        bool dayBeforeIsDue = false, bool startingSoonIsDue = false)
    {
        await using var db = NewDb(tenant);

        var interview = Interview.Schedule(
            applicationId, InterviewType.Technical, scheduledAtUtc, Duration,
            [Guid.NewGuid()], DateTime.UtcNow);
        db.Interviews.Add(interview);
        await db.SaveChangesAsync();

        var past = DateTime.UtcNow.AddMinutes(-1);
        if (dayBeforeIsDue)
        {
            await db.Interviews.Where(i => i.Id == interview.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.DayBeforeReminderDueAtUtc, past));
        }

        if (startingSoonIsDue)
        {
            await db.Interviews.Where(i => i.Id == interview.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.StartingSoonReminderDueAtUtc, past));
        }

        return interview.Id;
    }

    private async Task<Interview> LoadAsync(FixedTenant tenant, Guid interviewId)
    {
        await using var db = NewDb(tenant);
        return await db.Interviews.AsNoTracking().SingleAsync(i => i.Id == interviewId);
    }

    private static ApplicationForScheduling SomeApplication() =>
        new(Guid.NewGuid(), IsActive: true, Guid.NewGuid(), "Staff Engineer",
            Guid.NewGuid(), Guid.NewGuid(), "reminded@acme.test", "Remi");

    private InterviewsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildInterviewsOptions(_fixture.ConnectionString, tenant), tenant);
}
