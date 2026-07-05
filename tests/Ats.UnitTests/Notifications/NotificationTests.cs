using Ats.Modules.Notifications.Domain;

namespace Ats.UnitTests.Notifications;

public sealed class NotificationTests
{
    [Fact]
    public void should_create_unread_candidate_notification()
    {
        // Act
        var accountId = Guid.NewGuid();
        var notification = Notification.ForCandidate(
            accountId, NotificationType.ApplicationStageChanged, """{"jobTitle":"Engineer"}""");

        // Assert — addressed to the account, global (no tenant), born unread
        Assert.Equal(NotificationRecipientType.Candidate, notification.RecipientType);
        Assert.Equal(accountId, notification.RecipientId);
        Assert.Null(notification.TenantId);
        Assert.Null(notification.ReadAtUtc);
    }

    [Fact]
    public void should_reject_empty_recipient_or_payload()
    {
        Assert.Throws<ArgumentException>(() => Notification.ForCandidate(
            Guid.Empty, NotificationType.ApplicationStageChanged, """{"a":1}"""));
        Assert.Throws<ArgumentException>(() => Notification.ForCandidate(
            Guid.NewGuid(), NotificationType.ApplicationStageChanged, "  "));
    }

    [Fact]
    public void should_create_unread_company_user_notification_carrying_its_tenant()
    {
        // Act
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var notification = Notification.ForCompanyUser(
            tenantId, userId, NotificationType.NewApplication, """{"jobTitle":"Engineer"}""");

        // Assert — addressed to the user, but tagged with the tenant unlike a candidate row
        Assert.Equal(NotificationRecipientType.CompanyUser, notification.RecipientType);
        Assert.Equal(userId, notification.RecipientId);
        Assert.Equal(tenantId, notification.TenantId);
        Assert.Null(notification.ReadAtUtc);
    }

    [Fact]
    public void should_reject_empty_tenant_or_user_for_company_recipient()
    {
        Assert.Throws<ArgumentException>(() => Notification.ForCompanyUser(
            Guid.Empty, Guid.NewGuid(), NotificationType.NewApplication, """{"a":1}"""));
        Assert.Throws<ArgumentException>(() => Notification.ForCompanyUser(
            Guid.NewGuid(), Guid.Empty, NotificationType.NewApplication, """{"a":1}"""));
    }

    [Fact]
    public void should_keep_the_first_read_timestamp_when_marked_twice()
    {
        // Arrange
        var notification = Notification.ForCandidate(
            Guid.NewGuid(), NotificationType.InterviewScheduled, """{"a":1}""");

        // Act
        notification.MarkRead();
        var firstReadAt = notification.ReadAtUtc;
        notification.MarkRead();

        // Assert
        Assert.NotNull(firstReadAt);
        Assert.Equal(firstReadAt, notification.ReadAtUtc);
    }
}
