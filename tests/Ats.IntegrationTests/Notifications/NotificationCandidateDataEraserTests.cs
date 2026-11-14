using Ats.IntegrationTests.Interviews;
using Ats.IntegrationTests.Shared;
using Ats.Modules.Notifications.Domain;
using Ats.Modules.Notifications.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ats.IntegrationTests.Notifications;

[Collection("Integration")]
public sealed class NotificationCandidateDataEraserTests
{
    private readonly PostgresContainerFixture _fixture;
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly Guid _applicationId = Guid.NewGuid();
    private readonly FakeApplicationDirectory _applications = new(application: null);

    public NotificationCandidateDataEraserTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
        _applications.ApplicationIdsByCandidateAccount[_accountId] = [_applicationId];
    }

    // Found by recipient, not by application: a notification doesn't have to be about one.
    [Fact]
    public async Task should_delete_the_candidates_own_notifications()
    {
        const string payloadWithoutApplication = """{"jobTitle":"Platform Engineer"}""";
        var own = await AddAsync(Notification.ForCandidate(
            _accountId, NotificationType.ApplicationStageChanged, payloadWithoutApplication));
        var someoneElses = await AddAsync(Notification.ForCandidate(
            Guid.NewGuid(), NotificationType.ApplicationStageChanged, payloadWithoutApplication));

        await EraseAsync();

        Assert.False(await ExistsAsync(own));
        Assert.True(await ExistsAsync(someoneElses));
    }

    // The new-application notification names the candidate to every member of the company.
    [Fact]
    public async Task should_delete_the_company_notifications_about_the_candidates_applications()
    {
        var tenantId = Guid.NewGuid();
        var aboutTheCandidate = await AddAsync(NewApplication(tenantId, _applicationId, "Mira", "Tolun"));
        var aboutSomeoneElse = await AddAsync(NewApplication(tenantId, Guid.NewGuid(), "Deniz", "Kaya"));

        await EraseAsync();

        Assert.False(await ExistsAsync(aboutTheCandidate));
        Assert.True(await ExistsAsync(aboutSomeoneElse));
    }

    private async Task EraseAsync()
    {
        await using var db = NewDb();
        await new NotificationCandidateDataEraser(db, _applications).EraseAsync(_accountId);
    }

    private static Notification NewApplication(Guid tenantId, Guid applicationId, string firstName, string lastName) =>
        Notification.ForCompanyUser(
            tenantId, Guid.NewGuid(), NotificationType.NewApplication,
            NotificationPayloads.Serialize(
                new NotificationPayloads.NewApplication(applicationId, "Platform Engineer", firstName, lastName)));

    private async Task<Guid> AddAsync(Notification notification)
    {
        await using var db = NewDb();
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        return notification.Id;
    }

    private async Task<bool> ExistsAsync(Guid notificationId)
    {
        await using var db = NewDb();
        return await db.Notifications.AnyAsync(n => n.Id == notificationId);
    }

    private NotificationsDbContext NewDb() =>
        new(PostgresContainerFixture.BuildNotificationsOptions(_fixture.ConnectionString));
}
