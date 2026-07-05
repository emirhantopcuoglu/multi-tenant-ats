using Ats.IntegrationTests.Shared;
using Ats.Modules.Notifications.Application;
using Ats.Modules.Notifications.Application.Notifications;
using Ats.Modules.Notifications.Domain;
using Ats.Modules.Notifications.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ats.IntegrationTests.Notifications;

[Collection("Integration")]
public sealed class MarkNotificationReadTests
{
    private readonly PostgresContainerFixture _fixture;

    public MarkNotificationReadTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_mark_own_notification_read_and_stay_idempotent()
    {
        // Arrange
        var recipientId = Guid.NewGuid();
        Notification notification;
        await using (var db = NewDb())
        {
            notification = Notification.ForCandidate(
                recipientId, NotificationType.ApplicationStageChanged, """{"a":1}""");
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();
        }

        // Act — mark it twice, as a double-click would
        await using var handlerDb = NewDb();
        var handler = new MarkNotificationReadHandler(handlerDb);
        var command = new MarkNotificationReadCommand(
            NotificationRecipientType.Candidate, recipientId, notification.Id);
        var first = await handler.Handle(command, CancellationToken.None);
        var firstReadAt = await ReadAtOf(notification.Id);
        var second = await handler.Handle(command, CancellationToken.None);

        // Assert — both calls succeed and the original timestamp survives the second one
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotNull(firstReadAt);
        Assert.Equal(firstReadAt, await ReadAtOf(notification.Id));
    }

    [Fact]
    public async Task should_return_not_found_for_someone_elses_notification()
    {
        // Arrange — the row exists, but belongs to another candidate
        Notification notification;
        await using (var db = NewDb())
        {
            notification = Notification.ForCandidate(
                Guid.NewGuid(), NotificationType.ApplicationStageChanged, """{"a":1}""");
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();
        }

        // Act — a different recipient probes the id
        await using var handlerDb = NewDb();
        var result = await new MarkNotificationReadHandler(handlerDb).Handle(
            new MarkNotificationReadCommand(
                NotificationRecipientType.Candidate, Guid.NewGuid(), notification.Id),
            CancellationToken.None);

        // Assert — indistinguishable from a nonexistent id, and the row stays unread
        Assert.False(result.IsSuccess);
        Assert.Equal(NotificationErrors.NotFound.Code, result.Error.Code);
        Assert.Null(await ReadAtOf(notification.Id));
    }

    [Fact]
    public async Task should_mark_all_read_only_for_the_caller()
    {
        // Arrange — two unread for the owner, one unread for a stranger
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        await using (var db = NewDb())
        {
            db.Notifications.Add(Notification.ForCandidate(
                ownerId, NotificationType.ApplicationStageChanged, """{"a":1}"""));
            db.Notifications.Add(Notification.ForCandidate(
                ownerId, NotificationType.InterviewScheduled, """{"a":2}"""));
            db.Notifications.Add(Notification.ForCandidate(
                strangerId, NotificationType.ApplicationStageChanged, """{"a":3}"""));
            await db.SaveChangesAsync();
        }

        // Act
        await using var handlerDb = NewDb();
        var result = await new MarkAllNotificationsReadHandler(handlerDb).Handle(
            new MarkAllNotificationsReadCommand(NotificationRecipientType.Candidate, ownerId),
            CancellationToken.None);

        // Assert — both owner rows updated, the stranger's untouched
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value);
        await using var readDb = NewDb();
        Assert.Equal(0, await readDb.Notifications
            .CountAsync(n => n.RecipientId == ownerId && n.ReadAtUtc == null));
        Assert.Equal(1, await readDb.Notifications
            .CountAsync(n => n.RecipientId == strangerId && n.ReadAtUtc == null));
    }

    private async Task<DateTime?> ReadAtOf(Guid notificationId)
    {
        await using var db = NewDb();
        return (await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == notificationId)).ReadAtUtc;
    }

    private NotificationsDbContext NewDb() =>
        new(PostgresContainerFixture.BuildNotificationsOptions(_fixture.ConnectionString));
}
