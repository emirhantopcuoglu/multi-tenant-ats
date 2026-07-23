using Ats.IntegrationTests.Shared;
using Ats.Modules.Interviews.Application;
using Ats.Modules.Interviews.Application.Interviews;
using Ats.Modules.Interviews.Domain;
using Ats.Modules.Interviews.Infrastructure;
using Ats.Shared.Contracts.Applications;

namespace Ats.IntegrationTests.Interviews;

// Covers the double-booking guard end to end against a real Postgres, exercised through the
// ScheduleInterviewHandler (the entry point recruiters actually use). The FakeApplicationDirectory
// supplies the candidate/application mapping the guard needs; the interviews themselves are real
// rows so the overlap query runs for real.
[Collection("Integration")]
public sealed class InterviewConflictTests
{
    private static readonly DateTime BaseTime = DateTime.UtcNow.AddDays(2).Date.AddHours(15); // 15:00

    private readonly PostgresContainerFixture _fixture;

    public InterviewConflictTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_reject_when_an_interviewer_already_has_an_overlapping_interview()
    {
        // Arrange — X is booked 15:00–15:15
        var tenant = new FixedTenant(Guid.NewGuid());
        var interviewer = Guid.NewGuid();
        await SeedInterviewAsync(tenant, BaseTime, durationMinutes: 15, [interviewer]);

        // Act — try to book X again 14:45–15:15 (overlaps), different application/candidate
        var result = await ScheduleAsync(
            tenant, start: BaseTime.AddMinutes(-15), durationMinutes: 30, interviewers: [interviewer]);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(InterviewErrors.InterviewerConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task should_reject_when_the_candidate_already_has_an_overlapping_interview()
    {
        // Arrange — the candidate has one application with an interview at 15:00–15:15
        var tenant = new FixedTenant(Guid.NewGuid());
        var candidateId = Guid.NewGuid();
        var existingApplicationId = Guid.NewGuid();
        await SeedInterviewAsync(tenant, BaseTime, durationMinutes: 15, [Guid.NewGuid()], existingApplicationId);

        // Act — a second application for the SAME candidate, a different interviewer, overlapping time
        var newApplicationId = Guid.NewGuid();
        var directory = new FakeApplicationDirectory(ActiveApplication(newApplicationId, candidateId));
        directory.ApplicationIdsByCandidate[candidateId] = [existingApplicationId, newApplicationId];

        var result = await ScheduleAsync(
            tenant, start: BaseTime, durationMinutes: 30, interviewers: [Guid.NewGuid()],
            applicationId: newApplicationId, directory: directory);

        // Assert — no interviewer clash, but the candidate can't be in two interviews at once
        Assert.False(result.IsSuccess);
        Assert.Equal(InterviewErrors.CandidateConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task should_allow_a_back_to_back_interview_for_the_same_interviewer()
    {
        // Arrange — X is booked 15:00–15:15
        var tenant = new FixedTenant(Guid.NewGuid());
        var interviewer = Guid.NewGuid();
        await SeedInterviewAsync(tenant, BaseTime, durationMinutes: 15, [interviewer]);

        // Act — X again starting exactly at 15:15 (touching, not overlapping)
        var result = await ScheduleAsync(
            tenant, start: BaseTime.AddMinutes(15), durationMinutes: 30, interviewers: [interviewer]);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task should_reject_an_interview_starting_at_the_same_time()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var interviewer = Guid.NewGuid();
        await SeedInterviewAsync(tenant, BaseTime, durationMinutes: 30, [interviewer]);

        var result = await ScheduleAsync(
            tenant, start: BaseTime, durationMinutes: 15, interviewers: [interviewer]);

        Assert.False(result.IsSuccess);
        Assert.Equal(InterviewErrors.InterviewerConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task should_ignore_a_cancelled_interview_when_checking_conflicts()
    {
        // Arrange — X's overlapping interview exists but is cancelled
        var tenant = new FixedTenant(Guid.NewGuid());
        var interviewer = Guid.NewGuid();
        var existing = await SeedInterviewAsync(tenant, BaseTime, durationMinutes: 60, [interviewer]);
        await using (var db = NewDb(tenant))
        {
            var tracked = await db.Interviews.FindAsync(existing.Id);
            tracked!.Cancel();
            await db.SaveChangesAsync();
        }

        // Act — same interviewer, overlapping time
        var result = await ScheduleAsync(
            tenant, start: BaseTime, durationMinutes: 30, interviewers: [interviewer]);

        // Assert — a settled interview frees the slot
        Assert.True(result.IsSuccess);
    }

    private async Task<Interview> SeedInterviewAsync(
        FixedTenant tenant, DateTime start, int durationMinutes, IReadOnlyCollection<Guid> interviewers,
        Guid? applicationId = null)
    {
        var interview = Interview.Schedule(
            applicationId ?? Guid.NewGuid(), InterviewType.Technical, start, durationMinutes, interviewers);
        await using var db = NewDb(tenant);
        db.Interviews.Add(interview);
        await db.SaveChangesAsync();
        return interview;
    }

    private async Task<Ats.Shared.Kernel.Result<Guid>> ScheduleAsync(
        FixedTenant tenant, DateTime start, int durationMinutes, IReadOnlyList<Guid> interviewers,
        Guid? applicationId = null, FakeApplicationDirectory? directory = null)
    {
        var appId = applicationId ?? Guid.NewGuid();
        directory ??= new FakeApplicationDirectory(ActiveApplication(appId, Guid.NewGuid()));

        await using var db = NewDb(tenant);
        var handler = new ScheduleInterviewHandler(db, directory, new CapturingPublisher(), tenant);
        return await handler.Handle(
            new ScheduleInterviewCommand(appId, InterviewType.Technical, start, durationMinutes, interviewers, null),
            CancellationToken.None);
    }

    private static ApplicationForScheduling ActiveApplication(Guid applicationId, Guid candidateId) =>
        new(applicationId, IsActive: true, Guid.NewGuid(), "Staff Engineer",
            candidateId, Guid.NewGuid(), "candidate@acme.test", "Cand");

    private InterviewsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildInterviewsOptions(_fixture.ConnectionString, tenant), tenant);
}
