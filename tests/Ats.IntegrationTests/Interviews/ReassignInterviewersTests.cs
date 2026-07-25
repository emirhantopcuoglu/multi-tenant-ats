using Ats.IntegrationTests.Shared;
using Ats.Modules.Interviews.Application;
using Ats.Modules.Interviews.Application.Interviews;
using Ats.Modules.Interviews.Domain;
using Ats.Modules.Interviews.Infrastructure;

namespace Ats.IntegrationTests.Interviews;

// Covers swapping an interview's panel against a real Postgres. An integration test rather than a
// unit one because the interesting behaviour is the conflict query: the handler reuses the same
// guard as scheduling, but with the interview's *existing* slot and with itself excluded — get the
// exclusion wrong and every reassignment conflicts with the interview being reassigned.
[Collection("Integration")]
public sealed class ReassignInterviewersTests
{
    private static readonly DateTime BaseTime = DateTime.UtcNow.AddDays(2).Date.AddHours(15);

    private readonly PostgresContainerFixture _fixture;

    public ReassignInterviewersTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_replace_the_panel()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var interview = await SeedInterviewAsync(tenant, BaseTime, [Guid.NewGuid()]);
        var replacement = Guid.NewGuid();

        var result = await ReassignAsync(tenant, interview.Id, [replacement]);

        Assert.True(result.IsSuccess);
        await using var db = NewDb(tenant);
        var reloaded = await db.Interviews.FindAsync(interview.Id);
        Assert.Equal([replacement], reloaded!.InterviewerUserIds);
    }

    [Fact]
    public async Task should_not_conflict_with_the_interview_being_reassigned()
    {
        // The regression this guards: the interview occupies its own slot, so a guard that failed to
        // exclude it would reject keeping any interviewer who is already on it.
        var tenant = new FixedTenant(Guid.NewGuid());
        var existing = Guid.NewGuid();
        var added = Guid.NewGuid();
        var interview = await SeedInterviewAsync(tenant, BaseTime, [existing]);

        var result = await ReassignAsync(tenant, interview.Id, [existing, added]);

        Assert.True(result.IsSuccess);
        await using var db = NewDb(tenant);
        var reloaded = await db.Interviews.FindAsync(interview.Id);
        Assert.Equal(2, reloaded!.InterviewerUserIds.Count);
    }

    [Fact]
    public async Task should_reject_an_interviewer_already_booked_elsewhere_at_that_time()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var busy = Guid.NewGuid();
        // Someone else's interview overlapping the same slot.
        await SeedInterviewAsync(tenant, BaseTime.AddMinutes(15), [busy]);
        var interview = await SeedInterviewAsync(tenant, BaseTime, [Guid.NewGuid()]);

        var result = await ReassignAsync(tenant, interview.Id, [busy]);

        Assert.False(result.IsSuccess);
        Assert.Equal(InterviewErrors.InterviewerConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task should_allow_an_interviewer_booked_at_a_non_overlapping_time()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var interviewer = Guid.NewGuid();
        // Back-to-back, not overlapping: the guard uses half-open intervals.
        await SeedInterviewAsync(tenant, BaseTime.AddMinutes(60), [interviewer]);
        var interview = await SeedInterviewAsync(tenant, BaseTime, [Guid.NewGuid()]);

        var result = await ReassignAsync(tenant, interview.Id, [interviewer]);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task should_refuse_once_the_interview_has_started()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var interview = await SeedInterviewAsync(tenant, DateTime.UtcNow.AddHours(-3), [Guid.NewGuid()]);

        var result = await ReassignAsync(tenant, interview.Id, [Guid.NewGuid()]);

        Assert.False(result.IsSuccess);
        Assert.Equal("interview.transition_not_allowed", result.Error.Code);
    }

    [Fact]
    public async Task should_report_a_missing_interview()
    {
        var tenant = new FixedTenant(Guid.NewGuid());

        var result = await ReassignAsync(tenant, Guid.NewGuid(), [Guid.NewGuid()]);

        Assert.False(result.IsSuccess);
        Assert.Equal(InterviewErrors.NotFound.Code, result.Error.Code);
    }

    private async Task<Ats.Shared.Kernel.Result<bool>> ReassignAsync(
        FixedTenant tenant, Guid interviewId, IReadOnlyList<Guid> interviewerUserIds)
    {
        await using var db = NewDb(tenant);
        var handler = new ReassignInterviewersHandler(db);
        return await handler.Handle(
            new ReassignInterviewersCommand(interviewId, interviewerUserIds), CancellationToken.None);
    }

    // Booked a day ahead of its slot so a deliberately-past start still satisfies the
    // minimum-lead-time rule that applies at booking time.
    private async Task<Interview> SeedInterviewAsync(
        FixedTenant tenant, DateTime start, IReadOnlyCollection<Guid> interviewers)
    {
        var interview = Interview.Schedule(
            Guid.NewGuid(), InterviewType.Technical, start, 30, interviewers,
            nowUtc: start.AddDays(-1));

        await using var db = NewDb(tenant);
        db.Interviews.Add(interview);
        await db.SaveChangesAsync();
        return interview;
    }

    private InterviewsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildInterviewsOptions(_fixture.ConnectionString, tenant), tenant);
}
