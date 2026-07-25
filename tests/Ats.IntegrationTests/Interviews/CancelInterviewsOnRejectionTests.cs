using Ats.IntegrationTests.Shared;
using Ats.Modules.Interviews.Domain;
using Ats.Modules.Interviews.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ats.IntegrationTests.Interviews;

// Rejecting an application used to leave its booked interviews on the calendar — interviewers held
// the slot, the conflict guard treated it as occupied, and the candidate kept an invitation to a
// meeting nobody meant to hold.
[Collection("Integration")]
public sealed class CancelInterviewsOnRejectionTests
{
    private readonly PostgresContainerFixture _fixture;

    public CancelInterviewsOnRejectionTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_cancel_upcoming_interviews()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var applicationId = Guid.NewGuid();
        var interview = await SeedAsync(tenant, applicationId, DateTime.UtcNow.AddDays(2));

        var cancelled = await CancelAsync(tenant, applicationId);

        Assert.Equal(1, cancelled);
        var stored = await LoadAsync(tenant, interview.Id);
        Assert.Equal(InterviewStatus.Cancelled, stored.Status);
        Assert.Equal(InterviewCancellationReason.ApplicationRejected, stored.CancellationReason);
    }

    [Fact]
    public async Task should_leave_an_interview_that_already_happened_alone()
    {
        // An elapsed interview is a fact. Rejecting the application afterwards does not un-hold it,
        // and it still needs an honest outcome recorded.
        var tenant = new FixedTenant(Guid.NewGuid());
        var applicationId = Guid.NewGuid();
        var interview = await SeedAsync(tenant, applicationId, DateTime.UtcNow.AddHours(-3));

        var cancelled = await CancelAsync(tenant, applicationId);

        Assert.Equal(0, cancelled);
        var stored = await LoadAsync(tenant, interview.Id);
        Assert.Equal(InterviewStatus.Scheduled, stored.Status);
        Assert.Null(stored.CancellationReason);
    }

    [Fact]
    public async Task should_leave_already_settled_interviews_alone()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var applicationId = Guid.NewGuid();
        var slot = DateTime.UtcNow.AddDays(2);
        var interview = await SeedAsync(tenant, applicationId, slot);

        await using (var db = NewDb(tenant))
        {
            var tracked = await db.Interviews.FindAsync(interview.Id);
            tracked!.Cancel(InterviewCancellationReason.CandidateRequested, null, DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        var cancelled = await CancelAsync(tenant, applicationId);

        Assert.Equal(0, cancelled);
        // The original reason must survive — the system must not overwrite why a human cancelled.
        var stored = await LoadAsync(tenant, interview.Id);
        Assert.Equal(InterviewCancellationReason.CandidateRequested, stored.CancellationReason);
    }

    [Fact]
    public async Task should_only_touch_the_rejected_application()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var rejected = Guid.NewGuid();
        var untouched = Guid.NewGuid();
        await SeedAsync(tenant, rejected, DateTime.UtcNow.AddDays(2));
        var other = await SeedAsync(tenant, untouched, DateTime.UtcNow.AddDays(2));

        await CancelAsync(tenant, rejected);

        var stored = await LoadAsync(tenant, other.Id);
        Assert.Equal(InterviewStatus.Scheduled, stored.Status);
    }

    [Fact]
    public async Task should_not_reach_into_another_tenant()
    {
        var owner = new FixedTenant(Guid.NewGuid());
        var applicationId = Guid.NewGuid();
        var interview = await SeedAsync(owner, applicationId, DateTime.UtcNow.AddDays(2));

        // Same application id, a stranger's tenant on the message: nothing may move.
        var cancelled = await CancelAsync(new FixedTenant(Guid.NewGuid()), applicationId);

        Assert.Equal(0, cancelled);
        var stored = await LoadAsync(owner, interview.Id);
        Assert.Equal(InterviewStatus.Scheduled, stored.Status);
    }

    [Fact]
    public async Task should_be_idempotent()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var applicationId = Guid.NewGuid();
        await SeedAsync(tenant, applicationId, DateTime.UtcNow.AddDays(2));

        Assert.Equal(1, await CancelAsync(tenant, applicationId));
        // At-least-once delivery: a redelivery must find nothing left to do.
        Assert.Equal(0, await CancelAsync(tenant, applicationId));
    }

    private async Task<int> CancelAsync(FixedTenant tenant, Guid applicationId)
    {
        await using var db = NewDb(tenant);
        var consumer = new CancelInterviewsOnRejectionConsumer(
            db, NullLogger<CancelInterviewsOnRejectionConsumer>.Instance);
        return await consumer.CancelAsync(
            applicationId, tenant.TenantId!.Value, CancellationToken.None);
    }

    private async Task<Interview> SeedAsync(FixedTenant tenant, Guid applicationId, DateTime slot)
    {
        var interview = Interview.Schedule(
            applicationId, InterviewType.Technical, slot, 30, [Guid.NewGuid()],
            nowUtc: slot.AddDays(-1));

        await using var db = NewDb(tenant);
        db.Interviews.Add(interview);
        await db.SaveChangesAsync();
        return interview;
    }

    private async Task<Interview> LoadAsync(FixedTenant tenant, Guid interviewId)
    {
        await using var db = NewDb(tenant);
        return await db.Interviews.AsNoTracking().FirstAsync(i => i.Id == interviewId);
    }

    private InterviewsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildInterviewsOptions(_fixture.ConnectionString, tenant), tenant);
}
