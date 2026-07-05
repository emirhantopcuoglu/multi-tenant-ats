using Ats.IntegrationTests.Shared;
using Ats.Modules.Notifications.Application.Notifications;
using Ats.Modules.Notifications.Domain;
using Ats.Modules.Notifications.Infrastructure;

namespace Ats.IntegrationTests.Notifications;

[Collection("Integration")]
public sealed class CandidateNotificationQueriesTests
{
    private readonly PostgresContainerFixture _fixture;

    public CandidateNotificationQueriesTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_list_only_own_notifications_newest_first()
    {
        // Arrange — two recipients; only one is queried
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        await using (var db = NewDb())
        {
            db.Notifications.Add(Notification.ForCandidate(
                ownerId, NotificationType.ApplicationStageChanged, """{"jobTitle":"Older"}"""));
            // CreatedAtUtc is stamped at construction; the pause keeps the two timestamps strictly
            // ordered so the newest-first assertion cannot flake on clock resolution.
            await Task.Delay(20);
            db.Notifications.Add(Notification.ForCandidate(
                ownerId, NotificationType.InterviewScheduled, """{"jobTitle":"Newer"}"""));
            db.Notifications.Add(Notification.ForCandidate(
                strangerId, NotificationType.ApplicationStageChanged, """{"jobTitle":"NotYours"}"""));
            await db.SaveChangesAsync();
        }

        // Act
        await using var queryDb = NewDb();
        var result = await new ListNotificationsHandler(queryDb).Handle(
            new ListNotificationsQuery(NotificationRecipientType.Candidate, ownerId),
            CancellationToken.None);

        // Assert — the stranger's row is absent and the payload comes back as a JSON object
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.All(result.Value.Items, n => Assert.NotEqual("NotYours", GetJobTitle(n)));
        var newest = result.Value.Items[0];
        Assert.Equal(nameof(NotificationType.InterviewScheduled), newest.Type);
        Assert.Equal("Newer", GetJobTitle(newest));
        Assert.Null(newest.ReadAtUtc);
    }

    [Fact]
    public async Task should_page_the_feed()
    {
        // Arrange
        var recipientId = Guid.NewGuid();
        await using (var db = NewDb())
        {
            for (var i = 0; i < 3; i++)
                db.Notifications.Add(Notification.ForCandidate(
                    recipientId, NotificationType.ApplicationStageChanged, $$"""{"index":{{i}}}"""));
            await db.SaveChangesAsync();
        }

        // Act — page 2 of size 2 holds the single remaining row
        await using var queryDb = NewDb();
        var result = await new ListNotificationsHandler(queryDb).Handle(
            new ListNotificationsQuery(NotificationRecipientType.Candidate, recipientId, Page: 2, PageSize: 2),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.TotalCount);
        Assert.Equal(2, result.Value.TotalPages);
        Assert.Single(result.Value.Items);
    }

    [Fact]
    public async Task should_count_only_own_unread_notifications()
    {
        // Arrange — two unread + one read for the owner, one unread for a stranger
        var ownerId = Guid.NewGuid();
        await using (var db = NewDb())
        {
            db.Notifications.Add(Notification.ForCandidate(
                ownerId, NotificationType.ApplicationStageChanged, """{"a":1}"""));
            db.Notifications.Add(Notification.ForCandidate(
                ownerId, NotificationType.InterviewScheduled, """{"a":2}"""));
            var read = Notification.ForCandidate(
                ownerId, NotificationType.ApplicationStageChanged, """{"a":3}""");
            read.MarkRead();
            db.Notifications.Add(read);
            db.Notifications.Add(Notification.ForCandidate(
                Guid.NewGuid(), NotificationType.ApplicationStageChanged, """{"a":4}"""));
            await db.SaveChangesAsync();
        }

        // Act
        await using var queryDb = NewDb();
        var result = await new GetUnreadNotificationCountHandler(queryDb).Handle(
            new GetUnreadNotificationCountQuery(NotificationRecipientType.Candidate, ownerId),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value);
    }

    private static string? GetJobTitle(NotificationDto dto) =>
        dto.Payload.TryGetProperty("jobTitle", out var title) ? title.GetString() : null;

    private NotificationsDbContext NewDb() =>
        new(PostgresContainerFixture.BuildNotificationsOptions(_fixture.ConnectionString));
}
