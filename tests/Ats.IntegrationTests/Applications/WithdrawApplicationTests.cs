using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Application.Events;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ats.IntegrationTests.Applications;

// Application.Withdraw() existed and was unit-tested for a long time with nothing calling it. These
// tests cover the handler that finally does, and in particular the two things that only matter once
// a real request reaches it: a candidate request carries no ambient tenant, and the acting identity
// is a global account rather than a company user.
[Collection("Integration")]
public sealed class WithdrawApplicationTests
{
    private readonly PostgresContainerFixture _fixture;

    public WithdrawApplicationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_close_the_application_and_announce_it()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var applicationId = await SeedApplicationAsync(tenantId, accountId);

        // Act — no ambient tenant at all, which is what a candidate token actually gives us.
        var (result, publisher, _) = await WithdrawAsync(applicationId, accountId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ApplicationStatus.Withdrawn, await StatusOfAsync(tenantId, applicationId));

        var published = Assert.Single(publisher.Published.OfType<ApplicationWithdrawnEvent>());
        Assert.Equal(applicationId, published.ApplicationId);
        // The tenant must come off the stored row — the caller had none to give.
        Assert.Equal(tenantId, published.TenantId);
    }

    [Fact]
    public async Task should_log_the_withdrawal_against_the_applications_own_tenant()
    {
        // The ambient-tenant overload of AddAsync throws when nothing is resolved, and TryAddAsync
        // swallows that into a warning — so picking the wrong overload here would drop the entry from
        // both timelines while still returning success. Asserting the tenant catches that silently.
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var applicationId = await SeedApplicationAsync(tenantId, accountId);

        var (_, _, activityLog) = await WithdrawAsync(applicationId, accountId);

        var logged = Assert.Single(activityLog.Added);
        Assert.Equal(ApplicationActivityType.Withdrawn, logged.ActivityType);
        Assert.Equal(applicationId, logged.ApplicationId);
        // A withdrawal has no company-side actor, and the timeline must not imply one.
        Assert.Null(logged.ActorUserId);
        Assert.Equal(tenantId, Assert.Single(activityLog.AddedTenantIds));
    }

    [Fact]
    public async Task should_refuse_an_application_belonging_to_another_candidate()
    {
        // Arrange
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var applicationId = await SeedApplicationAsync(tenantId, owner);

        // Act
        var (result, publisher, activityLog) = await WithdrawAsync(applicationId, stranger);

        // Assert — NotFound, not Forbidden: a real id must be indistinguishable from a made-up one,
        // or the endpoint becomes an oracle for whether an application exists.
        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrors.NotFound.Code, result.Error.Code);
        Assert.Equal(ApplicationStatus.Active, await StatusOfAsync(tenantId, applicationId));
        Assert.Empty(publisher.Published);
        Assert.Empty(activityLog.Added);
    }

    [Fact]
    public async Task should_refuse_an_unknown_application()
    {
        var (result, _, _) = await WithdrawAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrors.NotFound.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Hired)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public async Task should_refuse_an_application_that_is_already_closed(ApplicationStatus status)
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var applicationId = await SeedApplicationAsync(tenantId, accountId, status);

        // Act
        var (result, publisher, _) = await WithdrawAsync(applicationId, accountId);

        // Assert — a distinct code from NotFound: this is the candidate's own application on a stale
        // tab, and the portal should say "already closed" rather than "no such application".
        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrors.NotWithdrawable.Code, result.Error.Code);
        Assert.Equal(status, await StatusOfAsync(tenantId, applicationId));
        // Nothing announced: a second message would cancel interviews for an unrelated later decision.
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task should_refuse_a_soft_deleted_application()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var applicationId = await SeedApplicationAsync(tenantId, accountId);

        var tenant = new FixedTenant(tenantId);
        await using (var db = NewDb(tenant))
        {
            var application = await db.Applications.SingleAsync(a => a.Id == applicationId);
            db.Applications.Remove(application);
            await db.SaveChangesAsync();
        }

        // Act
        var (result, _, _) = await WithdrawAsync(applicationId, accountId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrors.NotFound.Code, result.Error.Code);
    }

    // Runs the handler the way the controller does: no tenant in scope, the account id as the only
    // authorization input. Returns the collaborators so each test can assert on what was announced
    // and logged, not just on the row.
    private async Task<(Result<bool> Result, CapturingPublisher Publisher, InMemoryActivityLog ActivityLog)>
        WithdrawAsync(Guid applicationId, Guid candidateAccountId)
    {
        var publisher = new CapturingPublisher();
        var activityLog = new InMemoryActivityLog([]);

        await using var db = NewDb(new FixedTenant(null));
        var handler = new WithdrawApplicationHandler(
            db, publisher, activityLog, NullLogger<WithdrawApplicationHandler>.Instance);

        var result = await handler.Handle(
            new WithdrawApplicationCommand(candidateAccountId, applicationId), CancellationToken.None);

        return (result, publisher, activityLog);
    }

    private async Task<Guid> SeedApplicationAsync(
        Guid tenantId, Guid accountId, ApplicationStatus status = ApplicationStatus.Active)
    {
        var tenant = new FixedTenant(tenantId);
        await using var db = NewDb(tenant);

        var candidate = Candidate.Create($"{Guid.NewGuid():N}@acme.test", "Test", "Candidate");
        db.Candidates.Add(candidate);

        var application = Application.Create(
            jobId: Guid.NewGuid(), candidateId: candidate.Id, candidateAccountId: accountId,
            initialStageId: Guid.NewGuid(), cvFileKey: "cv/test.pdf");

        // The aggregate only allows a terminal status through its own transitions, which is exactly
        // how a real row would have got there.
        switch (status)
        {
            case ApplicationStatus.Rejected:
                application.Reject("Not a fit.", Guid.NewGuid());
                break;
            case ApplicationStatus.Hired:
                application.Hire(Guid.NewGuid());
                break;
            case ApplicationStatus.Withdrawn:
                application.Withdraw();
                break;
        }

        db.Applications.Add(application);
        await db.SaveChangesAsync();
        return application.Id;
    }

    private async Task<ApplicationStatus> StatusOfAsync(Guid tenantId, Guid applicationId)
    {
        await using var db = NewDb(new FixedTenant(tenantId));
        return await db.Applications
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.Id == applicationId)
            .Select(a => a.Status)
            .SingleAsync();
    }

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}
