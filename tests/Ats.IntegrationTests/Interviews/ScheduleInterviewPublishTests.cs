using Ats.IntegrationTests.Shared;
using Ats.Modules.Interviews.Application;
using Ats.Modules.Interviews.Application.Events;
using Ats.Modules.Interviews.Application.Interviews;
using Ats.Modules.Interviews.Domain;
using Ats.Modules.Interviews.Infrastructure;
using Ats.Shared.Contracts.Applications;
using Microsoft.EntityFrameworkCore;

namespace Ats.IntegrationTests.Interviews;

[Collection("Integration")]
public sealed class ScheduleInterviewPublishTests
{
    private readonly PostgresContainerFixture _fixture;

    public ScheduleInterviewPublishTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_publish_interview_scheduled_event_without_recruiter_notes()
    {
        // Arrange — an active application, as the Applications module would report it
        var tenant = new FixedTenant(Guid.NewGuid());
        var applicationId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        var candidateAccountId = Guid.NewGuid();
        var scheduledAt = DateTime.UtcNow.AddDays(3);

        var directory = new FakeApplicationDirectory(new ApplicationForScheduling(
            applicationId, IsActive: true, jobId, "Staff Engineer",
            candidateId, candidateAccountId, "invitee@acme.test", "Invi"));
        var publisher = new CapturingPublisher();

        // Act
        await using var db = NewDb(tenant);
        var handler = new ScheduleInterviewHandler(db, directory, publisher, tenant);
        var result = await handler.Handle(
            new ScheduleInterviewCommand(
                applicationId, InterviewType.Technical, scheduledAt, DurationMinutes: 60,
                Location: "Google Meet", [Guid.NewGuid()], Notes: "internal prep notes"),
            CancellationToken.None);

        // Assert — the interview is persisted and exactly one event describes it. The event type
        // has no Notes field at all, so the internal remark cannot leak by construction; this test
        // pins the rest of the payload.
        Assert.True(result.IsSuccess);
        var published = Assert.Single(publisher.Published);
        var scheduled = Assert.IsType<InterviewScheduledEvent>(published);
        Assert.Equal(result.Value, scheduled.InterviewId);
        Assert.Equal(applicationId, scheduled.ApplicationId);
        Assert.Equal(jobId, scheduled.JobId);
        Assert.Equal("Staff Engineer", scheduled.JobTitle);
        Assert.Equal(candidateId, scheduled.CandidateId);
        Assert.Equal(candidateAccountId, scheduled.CandidateAccountId);
        Assert.Equal("invitee@acme.test", scheduled.CandidateEmail);
        Assert.Equal("Invi", scheduled.CandidateFirstName);
        Assert.Equal(InterviewType.Technical, scheduled.Type);
        Assert.Equal(scheduledAt, scheduled.ScheduledAtUtc);
        Assert.Equal(60, scheduled.DurationMinutes);
        Assert.Equal("Google Meet", scheduled.Location);
        Assert.Equal(tenant.TenantId!.Value, scheduled.TenantId);

        await using var readDb = NewDb(tenant);
        Assert.NotNull(await readDb.Interviews.FirstOrDefaultAsync(i => i.Id == result.Value));
    }

    [Fact]
    public async Task should_not_publish_when_application_is_not_active()
    {
        // Arrange — a settled application: scheduling must fail before anything is announced
        var tenant = new FixedTenant(Guid.NewGuid());
        var applicationId = Guid.NewGuid();
        var directory = new FakeApplicationDirectory(new ApplicationForScheduling(
            applicationId, IsActive: false, Guid.NewGuid(), "Closed Role",
            Guid.NewGuid(), Guid.NewGuid(), "settled@acme.test", "Set"));
        var publisher = new CapturingPublisher();

        // Act
        await using var db = NewDb(tenant);
        var handler = new ScheduleInterviewHandler(db, directory, publisher, tenant);
        var result = await handler.Handle(
            new ScheduleInterviewCommand(
                applicationId, InterviewType.PhoneScreen, DateTime.UtcNow.AddDays(1), 30,
                null, [Guid.NewGuid()], null),
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(InterviewErrors.ApplicationNotActive.Code, result.Error.Code);
        Assert.Empty(publisher.Published);
    }

    private InterviewsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildInterviewsOptions(_fixture.ConnectionString, tenant), tenant);
}

// The Interviews module only ever sees applications through this port, so a canned answer is the
// honest fake: the enrichment itself is covered by GetForSchedulingTests on the real directory.
internal sealed class FakeApplicationDirectory : IApplicationDirectory
{
    private readonly ApplicationForScheduling? _application;

    public FakeApplicationDirectory(ApplicationForScheduling? application) => _application = application;

    public Task<ApplicationForScheduling?> GetForSchedulingAsync(
        Guid applicationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_application?.Id == applicationId ? _application : null);

    public Task<IReadOnlyDictionary<Guid, string>> GetCandidateNamesByApplicationAsync(
        IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

    public Task<int> CountApplicationsSinceAsync(
        DateTime sinceUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public Task<int> CountActiveCandidatesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}
