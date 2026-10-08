using Ats.IntegrationTests.Shared;
using Ats.Modules.Interviews.Domain;
using Ats.Modules.Interviews.Infrastructure;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ats.IntegrationTests.Interviews;

// An application reaching a terminal status used to leave its booked interviews on the calendar —
// interviewers held the slot, the conflict guard treated it as occupied, and the candidate kept an
// invitation to a meeting nobody meant to hold.
//
// The consumer serves both closing paths (company rejects / candidate withdraws) and the reason is
// the only thing that differs between them, so the reason-independent rules below are asserted once
// and the per-reason part is the theory at the top.
[Collection("Integration")]
public sealed class CancelInterviewsOnApplicationClosedTests
{
    private readonly PostgresContainerFixture _fixture;

    public CancelInterviewsOnApplicationClosedTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    // Driven through the real message types, not the shared CancelAsync(reason) core: which reason a
    // message maps to is the only per-path logic in this consumer, and calling the core with an
    // explicit reason would leave that mapping entirely uncovered — a swap of the two would then be
    // invisible to the suite while telling the candidate the wrong story in their cancellation email.
    [Fact]
    public async Task rejection_should_cancel_upcoming_interviews_as_ApplicationRejected()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var applicationId = Guid.NewGuid();
        var interview = await SeedAsync(tenant, applicationId, DateTime.UtcNow.AddDays(2));

        var cancelled = await ConsumeAsync(
            tenant, new ApplicationRejectedIntegrationEvent(
                applicationId, Guid.NewGuid(), "Staff Engineer", Guid.NewGuid(),
                "gone@acme.test", "Moved", tenant.TenantId!.Value));

        Assert.Equal(1, cancelled);
        var stored = await LoadAsync(tenant, interview.Id);
        Assert.Equal(InterviewStatus.Cancelled, stored.Status);
        Assert.Equal(InterviewCancellationReason.ApplicationRejected, stored.CancellationReason);
    }

    [Fact]
    public async Task withdrawal_should_cancel_upcoming_interviews_as_CandidateWithdrew()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var applicationId = Guid.NewGuid();
        var interview = await SeedAsync(tenant, applicationId, DateTime.UtcNow.AddDays(2));

        var cancelled = await ConsumeAsync(
            tenant, new ApplicationWithdrawnIntegrationEvent(applicationId, tenant.TenantId!.Value));

        Assert.Equal(1, cancelled);
        var stored = await LoadAsync(tenant, interview.Id);
        Assert.Equal(InterviewStatus.Cancelled, stored.Status);
        Assert.Equal(InterviewCancellationReason.CandidateWithdrew, stored.CancellationReason);
    }

    [Fact]
    public async Task should_leave_an_interview_that_already_happened_alone()
    {
        // An elapsed interview is a fact. Closing the application afterwards does not un-hold it,
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
    public async Task should_only_touch_the_closed_application()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var closed = Guid.NewGuid();
        var untouched = Guid.NewGuid();
        await SeedAsync(tenant, closed, DateTime.UtcNow.AddDays(2));
        var other = await SeedAsync(tenant, untouched, DateTime.UtcNow.AddDays(2));

        await CancelAsync(tenant, closed);

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

    private Task<int> ConsumeAsync(FixedTenant tenant, ApplicationRejectedIntegrationEvent message) =>
        RunAsync(tenant, (consumer, ct) => consumer.CancelAsync(message, ct));

    private Task<int> ConsumeAsync(FixedTenant tenant, ApplicationWithdrawnIntegrationEvent message) =>
        RunAsync(tenant, (consumer, ct) => consumer.CancelAsync(message, ct));

    // The reason-independent rules below all use the rejection path; which of the two they run through
    // is irrelevant to them, and the two tests above already pin the reasons.
    private Task<int> CancelAsync(FixedTenant tenant, Guid applicationId) =>
        ConsumeAsync(tenant, new ApplicationRejectedIntegrationEvent(
            applicationId, Guid.NewGuid(), "Staff Engineer", Guid.NewGuid(),
            "closed@acme.test", "Closed", tenant.TenantId!.Value));

    private async Task<int> RunAsync(
        FixedTenant tenant,
        Func<CancelInterviewsOnApplicationClosedConsumer, CancellationToken, Task<int>> act)
    {
        await using var db = NewDb(tenant);
        var consumer = new CancelInterviewsOnApplicationClosedConsumer(
            db, NullLogger<CancelInterviewsOnApplicationClosedConsumer>.Instance);
        return await act(consumer, CancellationToken.None);
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
