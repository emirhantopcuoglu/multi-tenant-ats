using Ats.IntegrationTests.Shared;
using Ats.Modules.Interviews.Application;
using Ats.Modules.Interviews.Application.Interviews;
using Ats.Modules.Interviews.Domain;
using Ats.Modules.Interviews.Infrastructure;
using Ats.Shared.Contracts.Applications;

namespace Ats.IntegrationTests.Interviews;

[Collection("Integration")]
public sealed class JoinInterviewRoomTests
{
    private readonly PostgresContainerFixture _fixture;

    public JoinInterviewRoomTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_allow_the_owning_candidate_when_the_room_is_open()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var candidateAccountId = Guid.NewGuid();
        // Inside the 10-minute lead window: RoomOpensAtUtc = now - 5 min, already open.
        var (interview, applications) = await SeedAsync(
            tenant, DateTime.UtcNow.AddMinutes(5), candidateAccountId: candidateAccountId);

        var handler = new JoinInterviewRoomHandler(NewDb(tenant), applications);
        var result = await handler.Handle(
            new JoinInterviewRoomQuery(interview.RoomToken!, candidateAccountId, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Open", result.Value.State);
    }

    [Fact]
    public async Task should_refuse_a_different_candidate()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var (interview, applications) = await SeedAsync(
            tenant, DateTime.UtcNow.AddMinutes(5), candidateAccountId: Guid.NewGuid());

        var handler = new JoinInterviewRoomHandler(NewDb(tenant), applications);
        var result = await handler.Handle(
            new JoinInterviewRoomQuery(interview.RoomToken!, Guid.NewGuid(), null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InterviewErrors.NotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task should_allow_an_assigned_interviewer_from_the_right_tenant()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var interviewerId = Guid.NewGuid();
        var (interview, applications) = await SeedAsync(
            tenant, DateTime.UtcNow.AddMinutes(5), interviewerUserIds: [interviewerId]);

        var handler = new JoinInterviewRoomHandler(NewDb(tenant), applications);
        var result = await handler.Handle(
            new JoinInterviewRoomQuery(interview.RoomToken!, null, interviewerId, tenant.TenantId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task should_refuse_a_company_user_from_a_different_tenant()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var interviewerId = Guid.NewGuid();
        var (interview, applications) = await SeedAsync(
            tenant, DateTime.UtcNow.AddMinutes(5), interviewerUserIds: [interviewerId]);

        var handler = new JoinInterviewRoomHandler(NewDb(tenant), applications);
        var result = await handler.Handle(
            new JoinInterviewRoomQuery(interview.RoomToken!, null, interviewerId, Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task should_report_too_early_before_the_lead_window()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var candidateAccountId = Guid.NewGuid();
        var (interview, applications) = await SeedAsync(
            tenant, DateTime.UtcNow.AddDays(1), candidateAccountId: candidateAccountId);

        var handler = new JoinInterviewRoomHandler(NewDb(tenant), applications);
        var result = await handler.Handle(
            new JoinInterviewRoomQuery(interview.RoomToken!, candidateAccountId, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("TooEarly", result.Value.State);
    }

    [Fact]
    public async Task should_report_unavailable_for_a_cancelled_interview()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var candidateAccountId = Guid.NewGuid();
        var (interview, applications) = await SeedAsync(
            tenant, DateTime.UtcNow.AddMinutes(5), candidateAccountId: candidateAccountId);

        await using (var db = NewDb(tenant))
        {
            var tracked = await db.Interviews.FindAsync(interview.Id);
            tracked!.Cancel(InterviewCancellationReason.Other, null, tracked.ScheduledAtUtc.AddMinutes(-1));
            await db.SaveChangesAsync();
        }

        var handler = new JoinInterviewRoomHandler(NewDb(tenant), applications);
        var result = await handler.Handle(
            new JoinInterviewRoomQuery(interview.RoomToken!, candidateAccountId, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Unavailable", result.Value.State);
    }

    [Fact]
    public async Task should_refuse_an_unknown_room_token()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var applications = new FakeApplicationDirectory(null);

        var handler = new JoinInterviewRoomHandler(NewDb(tenant), applications);
        var result = await handler.Handle(
            new JoinInterviewRoomQuery("does-not-exist", Guid.NewGuid(), null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InterviewErrors.NotFound.Code, result.Error.Code);
    }

    private async Task<(Interview Interview, FakeApplicationDirectory Applications)> SeedAsync(
        FixedTenant tenant,
        DateTime scheduledAtUtc,
        Guid? candidateAccountId = null,
        IReadOnlyCollection<Guid>? interviewerUserIds = null)
    {
        var applicationId = Guid.NewGuid();
        // Booked a day before it starts. These tests deliberately place interviews minutes away to
        // exercise the room window, which the minimum-lead-time rule would otherwise reject — so the
        // seed states when the booking was made rather than pretending it happened just now.
        var interview = Interview.Schedule(
            applicationId, InterviewType.Technical, scheduledAtUtc, 30,
            interviewerUserIds ?? [Guid.NewGuid()], nowUtc: scheduledAtUtc.AddDays(-1));

        await using (var db = NewDb(tenant))
        {
            db.Interviews.Add(interview);
            await db.SaveChangesAsync();
        }

        var application = new ApplicationForScheduling(
            applicationId, IsActive: true, Guid.NewGuid(), "Staff Engineer",
            Guid.NewGuid(), candidateAccountId, "candidate@acme.test", "Cand");

        return (interview, new FakeApplicationDirectory(application));
    }

    private InterviewsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildInterviewsOptions(_fixture.ConnectionString, tenant), tenant);
}
