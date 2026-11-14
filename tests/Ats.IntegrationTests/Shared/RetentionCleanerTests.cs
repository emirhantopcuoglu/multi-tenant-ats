using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Modules.Jobs.Infrastructure;
using Ats.Modules.Notifications.Domain;
using Ats.Modules.Notifications.Infrastructure;
using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.IntegrationTests.Shared;

// A row a day past its cutoff goes, a row a day short of it stays. The cutoffs are weeks apart, so a cleaner
// reading the wrong one fails too.
[Collection("Integration")]
public sealed class RetentionCleanerTests
{
    private static readonly DateTime AuditCutoff = DateTime.UtcNow.AddDays(-90);
    private static readonly DateTime NotificationCutoff = DateTime.UtcNow.AddDays(-60);
    private static readonly DateTime TokenCutoff = DateTime.UtcNow.AddDays(-30);
    private static readonly RetentionCutoffs Cutoffs = new(AuditCutoff, NotificationCutoff, TokenCutoff);

    private readonly PostgresContainerFixture _fixture;

    public RetentionCleanerTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task audit_rows_older_than_the_cutoff_should_go()
    {
        var tenantId = Guid.NewGuid();
        var old = AuditRow(tenantId, AuditCutoff.AddDays(-1));
        var recent = AuditRow(tenantId, AuditCutoff.AddDays(1));
        await using (var db = NewJobsDb())
        {
            db.Set<AuditLogEntry>().AddRange(old, recent);
            await db.SaveChangesAsync();
        }

        await using (var db = NewJobsDb())
            await new AuditLogRetentionCleaner<JobsDbContext>(db).CleanAsync(Cutoffs);

        await using var readDb = NewJobsDb();
        Assert.Equal(
            [recent.Id],
            await readDb.Set<AuditLogEntry>().Where(e => e.TenantId == tenantId).Select(e => e.Id).ToListAsync());
    }

    // A revoked session is kept until it expires: presenting it ends its whole family.
    [Fact]
    public async Task company_sessions_and_invitations_should_go_once_expired_before_the_cutoff()
    {
        var expired = RefreshToken.Issue(Guid.NewGuid(), $"old-{Guid.NewGuid()}", TokenCutoff.AddDays(-1));
        var revoked = RefreshToken.Issue(Guid.NewGuid(), $"revoked-{Guid.NewGuid()}", TokenCutoff.AddDays(1));
        revoked.Revoke();
        var tenantId = Guid.NewGuid();
        var oldInvitation = Invitation.Create("ece.tan@example.test", "Recruiter", $"old-{Guid.NewGuid()}", validDays: 7);
        oldInvitation.MarkAccepted();
        var recentInvitation = Invitation.Create("ece.tan@example.test", "Recruiter", $"new-{Guid.NewGuid()}", validDays: 7);
        await using (var db = NewTenantsDb(tenantId))
        {
            db.RefreshTokens.AddRange(expired, revoked);
            db.Invitations.AddRange(oldInvitation, recentInvitation);
            await db.SaveChangesAsync();
            await db.Invitations.Where(i => i.Id == oldInvitation.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.ExpiresAtUtc, TokenCutoff.AddDays(-1)));
        }

        await using (var db = NewTenantsDb(tenantId: null))
            await new TenantsRetentionCleaner(db).CleanAsync(Cutoffs);

        await using var readDb = NewTenantsDb(tenantId: null);
        var tokenIds = new[] { expired.Id, revoked.Id };
        Assert.Equal([revoked.Id], await readDb.RefreshTokens.Where(t => tokenIds.Contains(t.Id)).Select(t => t.Id).ToListAsync());
        Assert.Equal(
            [recentInvitation.Id],
            await readDb.Invitations.IgnoreQueryFilters().Where(i => i.TenantId == tenantId).Select(i => i.Id).ToListAsync());
    }

    [Fact]
    public async Task candidate_sessions_and_requests_should_go_once_expired_before_the_cutoff()
    {
        var account = CandidateAccount.Register(
            $"ece.tan.{Guid.NewGuid():N}@example.test", "hashed-password", "Ece", "Tan", SupportedLanguages.Default);
        var oldToken = CandidateRefreshToken.Issue(account.Id, $"old-{Guid.NewGuid()}", Guid.NewGuid(), TokenCutoff.AddDays(-1));
        var recentToken = CandidateRefreshToken.Issue(account.Id, $"new-{Guid.NewGuid()}", Guid.NewGuid(), TokenCutoff.AddDays(1));
        var oldChange = EmailChangeRequest.Create(account.Id, "ece.new@example.test", $"old-{Guid.NewGuid()}");
        var oldVerification = EmailVerificationRequest.Create(account.Id, $"old-{Guid.NewGuid()}");
        var oldReset = PasswordResetRequest.Create(account.Id, $"old-{Guid.NewGuid()}");
        await using (var db = NewCandidateAccountsDb())
        {
            db.CandidateAccounts.Add(account);
            db.CandidateRefreshTokens.AddRange(oldToken, recentToken);
            db.AddRange(oldChange, oldVerification, oldReset);
            db.AddRange(
                EmailChangeRequest.Create(account.Id, "ece.new@example.test", $"new-{Guid.NewGuid()}"),
                EmailVerificationRequest.Create(account.Id, $"new-{Guid.NewGuid()}"),
                PasswordResetRequest.Create(account.Id, $"new-{Guid.NewGuid()}"));
            await db.SaveChangesAsync();

            // The factories stamp the current time; the test dates the expiry back.
            var expiredAtUtc = TokenCutoff.AddDays(-1);
            await db.EmailChangeRequests.Where(r => r.Id == oldChange.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(r => r.ExpiresAtUtc, expiredAtUtc));
            await db.EmailVerificationRequests.Where(r => r.Id == oldVerification.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(r => r.ExpiresAtUtc, expiredAtUtc));
            await db.PasswordResetRequests.Where(r => r.Id == oldReset.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(r => r.ExpiresAtUtc, expiredAtUtc));
        }

        await using (var db = NewCandidateAccountsDb())
            await new CandidateAccountsRetentionCleaner(db).CleanAsync(Cutoffs);

        await using var readDb = NewCandidateAccountsDb();
        Assert.Equal([recentToken.Id], await readDb.CandidateRefreshTokens.Where(t => t.CandidateAccountId == account.Id).Select(t => t.Id).ToListAsync());
        Assert.False(await readDb.EmailChangeRequests.AnyAsync(r => r.Id == oldChange.Id));
        Assert.False(await readDb.EmailVerificationRequests.AnyAsync(r => r.Id == oldVerification.Id));
        Assert.False(await readDb.PasswordResetRequests.AnyAsync(r => r.Id == oldReset.Id));
        Assert.Equal(1, await readDb.EmailChangeRequests.CountAsync(r => r.CandidateAccountId == account.Id));
        Assert.Equal(1, await readDb.EmailVerificationRequests.CountAsync(r => r.CandidateAccountId == account.Id));
        Assert.Equal(1, await readDb.PasswordResetRequests.CountAsync(r => r.CandidateAccountId == account.Id));
    }

    // Unread ones stay: no period was agreed for them.
    [Fact]
    public async Task notifications_read_before_the_cutoff_should_go()
    {
        var recipientId = Guid.NewGuid();
        var readLongAgo = Notification.ForCandidate(recipientId, NotificationType.ApplicationViewed, """{"jobTitle":"Data Engineer"}""");
        var readRecently = Notification.ForCandidate(recipientId, NotificationType.ApplicationViewed, """{"jobTitle":"Data Engineer"}""");
        var unread = Notification.ForCandidate(recipientId, NotificationType.ApplicationViewed, """{"jobTitle":"Data Engineer"}""");
        readLongAgo.MarkRead();
        readRecently.MarkRead();
        await using (var db = NewNotificationsDb())
        {
            db.Notifications.AddRange(readLongAgo, readRecently, unread);
            await db.SaveChangesAsync();
            await db.Notifications.Where(n => n.Id == readLongAgo.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(n => n.ReadAtUtc, NotificationCutoff.AddDays(-1)));
            await db.Notifications.Where(n => n.Id == readRecently.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(n => n.ReadAtUtc, NotificationCutoff.AddDays(1)));
            await db.Notifications.Where(n => n.Id == unread.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(n => n.CreatedAtUtc, NotificationCutoff.AddDays(-1)));
        }

        await using (var db = NewNotificationsDb())
            await new NotificationRetentionCleaner(db).CleanAsync(Cutoffs);

        await using var readDb = NewNotificationsDb();
        Assert.Equal(
            new[] { readRecently.Id, unread.Id }.Order(),
            (await readDb.Notifications.Where(n => n.RecipientId == recipientId).Select(n => n.Id).ToListAsync()).Order());
    }

    private static AuditLogEntry AuditRow(Guid tenantId, DateTime occurredAtUtc) => new()
    {
        TenantId = tenantId,
        Action = "Updated",
        ResourceType = "Job",
        ResourceId = Guid.NewGuid().ToString(),
        OccurredAtUtc = occurredAtUtc
    };

    private JobsDbContext NewJobsDb()
    {
        var tenant = new FixedTenant(null);
        return new JobsDbContext(PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
    }

    private TenantsDbContext NewTenantsDb(Guid? tenantId)
    {
        var tenant = new FixedTenant(tenantId);
        return new TenantsDbContext(PostgresContainerFixture.BuildTenantsOptions(_fixture.ConnectionString, tenant), tenant);
    }

    private CandidateAccountsDbContext NewCandidateAccountsDb() =>
        new(PostgresContainerFixture.BuildCandidateAccountsOptions(_fixture.ConnectionString));

    private NotificationsDbContext NewNotificationsDb() =>
        new(PostgresContainerFixture.BuildNotificationsOptions(_fixture.ConnectionString));
}
