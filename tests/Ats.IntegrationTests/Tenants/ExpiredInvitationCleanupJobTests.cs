using Ats.IntegrationTests.Shared;
using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ats.IntegrationTests.Tenants;

[Collection("Integration")]
public sealed class ExpiredInvitationCleanupJobTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;

    public ExpiredInvitationCleanupJobTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Truncate before each test so rows from one test never affect the next.
        // The cleanup job uses IgnoreQueryFilters so it sees all rows globally;
        // without this reset each test would observe leftovers from its predecessor.
        await using var db = BuildTenantsContext(Guid.NewGuid());
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM tenants.\"Invitations\"");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task should_delete_expired_unaccepted_invitations()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedInvitationAsync(tenantId, "expired@example.com", validDays: -1, accepted: false);

        // Act
        await RunCleanupAsync(tenantId);

        // Assert
        await using var db = BuildTenantsContext(tenantId);
        var remaining = await db.Invitations
            .IgnoreQueryFilters()
            .CountAsync(i => i.IsDeleted == false);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task should_not_delete_valid_invitations()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedInvitationAsync(tenantId, "valid@example.com", validDays: 7, accepted: false);

        // Act
        await RunCleanupAsync(tenantId);

        // Assert — not yet expired, so it must survive the cleanup
        await using var db = BuildTenantsContext(tenantId);
        var remaining = await db.Invitations
            .IgnoreQueryFilters()
            .CountAsync();
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task should_not_delete_accepted_invitations()
    {
        // Arrange — expired but accepted (user already joined)
        var tenantId = Guid.NewGuid();
        await SeedInvitationAsync(tenantId, "accepted@example.com", validDays: -1, accepted: true);

        // Act
        await RunCleanupAsync(tenantId);

        // Assert — AcceptedAtUtc != null means the predicate does not match
        await using var db = BuildTenantsContext(tenantId);
        var remaining = await db.Invitations
            .IgnoreQueryFilters()
            .CountAsync();
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task should_be_idempotent()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedInvitationAsync(tenantId, "idempotent@example.com", validDays: -1, accepted: false);

        // Act — first run deletes the expired row
        await RunCleanupAsync(tenantId);

        // Act — second run must not throw and must report 0 deletions
        // (verified implicitly: no exception, table still empty)
        await RunCleanupAsync(tenantId);

        // Assert
        await using var db = BuildTenantsContext(tenantId);
        var remaining = await db.Invitations
            .IgnoreQueryFilters()
            .CountAsync();
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task should_delete_across_all_tenants()
    {
        // Arrange — two different tenants each have an expired invitation
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedInvitationAsync(tenantA, "a@example.com", validDays: -1, accepted: false);
        await SeedInvitationAsync(tenantB, "b@example.com", validDays: -1, accepted: false);

        // Act — job runs with a third, unrelated tenant in the current context;
        // IgnoreQueryFilters inside CleanupAsync makes it tenant-agnostic
        await RunCleanupAsync(Guid.NewGuid());

        // Assert — both rows gone despite belonging to different tenants
        await using var db = BuildTenantsContext(null);
        var remaining = await db.Invitations
            .IgnoreQueryFilters()
            .CountAsync();
        Assert.Equal(0, remaining);
    }

    private async Task SeedInvitationAsync(Guid tenantId, string email, int validDays, bool accepted)
    {
        await using var db = BuildTenantsContext(tenantId);
        var invitation = Invitation.Create(email, "Recruiter", $"hash-{Guid.NewGuid()}", validDays);
        if (accepted)
            invitation.MarkAccepted();
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync();
    }

    private async Task RunCleanupAsync(Guid runningAsTenantId)
    {
        // The cleanup job receives a DbContext whose ICurrentTenant is set to runningAsTenantId.
        // The job calls IgnoreQueryFilters() internally, so the actual tenant value is irrelevant —
        // but it reflects real usage where the background job runs inside a DI scope.
        await using var db = BuildTenantsContext(runningAsTenantId);
        var job = new ExpiredInvitationCleanupJob(db, NullLogger<ExpiredInvitationCleanupJob>.Instance);
        await job.CleanupAsync();
    }

    private TenantsDbContext BuildTenantsContext(Guid? tenantId)
    {
        var tenant = new FixedTenant(tenantId);
        return new TenantsDbContext(
            PostgresContainerFixture.BuildTenantsOptions(_fixture.ConnectionString, tenant), tenant);
    }
}
