using System.Text.Json;
using Ats.Modules.Notifications.Domain;
using Ats.Modules.Notifications.Infrastructure;
using Ats.Shared.Contracts.Notifications;

namespace Ats.UnitTests.Notifications;

// Pins the message-to-row mapping of the in-app notification consumers: which account the row is
// addressed to, which payload fields the frontend can rely on, and that account-less messages
// (applications older than candidate accounts) produce no row at all.
public sealed class NotificationConsumerMappingTests
{
    [Fact]
    public void should_build_stage_changed_notification_with_structured_payload()
    {
        // Arrange
        var candidateAccountId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var message = StageChangedMessage(candidateAccountId, applicationId);

        // Act
        var notification = ApplicationStageChangedNotificationConsumer.TryBuildNotification(message);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal(NotificationRecipientType.Candidate, notification.RecipientType);
        Assert.Equal(candidateAccountId, notification.RecipientId);
        Assert.Equal(NotificationType.ApplicationStageChanged, notification.Type);
        Assert.Null(notification.ReadAtUtc);

        using var payload = JsonDocument.Parse(notification.Payload);
        Assert.Equal(applicationId, payload.RootElement.GetProperty("applicationId").GetGuid());
        Assert.Equal("Staff Engineer", payload.RootElement.GetProperty("jobTitle").GetString());
        Assert.Equal("Applied", payload.RootElement.GetProperty("fromStageName").GetString());
        Assert.Equal("Screening", payload.RootElement.GetProperty("toStageName").GetString());
    }

    [Fact]
    public void should_skip_stage_changed_notification_without_candidate_account()
    {
        // Arrange — a legacy application: no marketplace account to deliver to
        var message = StageChangedMessage(candidateAccountId: null, Guid.NewGuid());

        // Act & Assert
        Assert.Null(ApplicationStageChangedNotificationConsumer.TryBuildNotification(message));
    }

    [Fact]
    public void should_build_interview_scheduled_notification_with_structured_payload()
    {
        // Arrange
        var candidateAccountId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var scheduledAt = new DateTime(2026, 7, 20, 14, 0, 0, DateTimeKind.Utc);
        var message = InterviewMessage(candidateAccountId, applicationId, scheduledAt);

        // Act
        var notification = InterviewScheduledNotificationConsumer.TryBuildNotification(message);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal(candidateAccountId, notification.RecipientId);
        Assert.Equal(NotificationType.InterviewScheduled, notification.Type);

        using var payload = JsonDocument.Parse(notification.Payload);
        Assert.Equal(applicationId, payload.RootElement.GetProperty("applicationId").GetGuid());
        Assert.Equal("Staff Engineer", payload.RootElement.GetProperty("jobTitle").GetString());
        Assert.Equal("Technical", payload.RootElement.GetProperty("interviewType").GetString());
        Assert.Equal(scheduledAt, payload.RootElement.GetProperty("scheduledAtUtc").GetDateTime());
        Assert.Equal(60, payload.RootElement.GetProperty("durationMinutes").GetInt32());
        Assert.Equal("room-token-abc", payload.RootElement.GetProperty("roomToken").GetString());
    }

    [Fact]
    public void should_skip_interview_notification_without_candidate_account()
    {
        // Arrange
        var message = InterviewMessage(
            candidateAccountId: null, Guid.NewGuid(), DateTime.UtcNow.AddDays(2));

        // Act & Assert
        Assert.Null(InterviewScheduledNotificationConsumer.TryBuildNotification(message));
    }

    [Fact]
    public void should_build_viewed_notification_with_structured_payload()
    {
        // Arrange
        var candidateAccountId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var message = ViewedMessage(candidateAccountId, applicationId);

        // Act
        var notification = ApplicationViewedNotificationConsumer.TryBuildNotification(message);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal(NotificationRecipientType.Candidate, notification.RecipientType);
        Assert.Equal(candidateAccountId, notification.RecipientId);
        Assert.Equal(NotificationType.ApplicationViewed, notification.Type);
        Assert.Null(notification.ReadAtUtc);

        using var payload = JsonDocument.Parse(notification.Payload);
        Assert.Equal(applicationId, payload.RootElement.GetProperty("applicationId").GetGuid());
        Assert.Equal("Staff Engineer", payload.RootElement.GetProperty("jobTitle").GetString());
    }

    [Fact]
    public void should_skip_viewed_notification_without_candidate_account()
    {
        // Arrange
        var message = ViewedMessage(candidateAccountId: null, Guid.NewGuid());

        // Act & Assert
        Assert.Null(ApplicationViewedNotificationConsumer.TryBuildNotification(message));
    }

    [Fact]
    public void should_build_cv_downloaded_notification_with_structured_payload()
    {
        // Arrange
        var candidateAccountId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var message = CvDownloadedMessage(candidateAccountId, applicationId);

        // Act
        var notification = ApplicationCvDownloadedNotificationConsumer.TryBuildNotification(message);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal(NotificationRecipientType.Candidate, notification.RecipientType);
        Assert.Equal(candidateAccountId, notification.RecipientId);
        Assert.Equal(NotificationType.ApplicationCvDownloaded, notification.Type);
        Assert.Null(notification.ReadAtUtc);

        using var payload = JsonDocument.Parse(notification.Payload);
        Assert.Equal(applicationId, payload.RootElement.GetProperty("applicationId").GetGuid());
        Assert.Equal("Staff Engineer", payload.RootElement.GetProperty("jobTitle").GetString());
    }

    [Fact]
    public void should_skip_cv_downloaded_notification_without_candidate_account()
    {
        // Arrange
        var message = CvDownloadedMessage(candidateAccountId: null, Guid.NewGuid());

        // Act & Assert
        Assert.Null(ApplicationCvDownloadedNotificationConsumer.TryBuildNotification(message));
    }

    [Fact]
    public void should_fan_out_one_new_application_notification_per_tenant_user()
    {
        // Arrange — three members of the tenant
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var userIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var message = NewApplicationMessage(tenantId, applicationId);

        // Act
        var notifications = NewApplicationNotificationConsumer.BuildNotifications(message, userIds);

        // Assert — one row per user, each addressed to that user and tagged with the tenant
        Assert.Equal(userIds.Length, notifications.Count);
        Assert.Equal(userIds.ToHashSet(), notifications.Select(n => n.RecipientId).ToHashSet());
        Assert.All(notifications, n =>
        {
            Assert.Equal(NotificationRecipientType.CompanyUser, n.RecipientType);
            Assert.Equal(tenantId, n.TenantId);
            Assert.Equal(NotificationType.NewApplication, n.Type);
            Assert.Null(n.ReadAtUtc);

            using var payload = JsonDocument.Parse(n.Payload);
            Assert.Equal(applicationId, payload.RootElement.GetProperty("applicationId").GetGuid());
            Assert.Equal("Staff Engineer", payload.RootElement.GetProperty("jobTitle").GetString());
            Assert.Equal("Jane", payload.RootElement.GetProperty("candidateFirstName").GetString());
            Assert.Equal("Doe", payload.RootElement.GetProperty("candidateLastName").GetString());
        });
    }

    [Fact]
    public void should_produce_no_notifications_when_tenant_has_no_users()
    {
        // Arrange — a tenant with (somehow) no members yet
        var message = NewApplicationMessage(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        Assert.Empty(NewApplicationNotificationConsumer.BuildNotifications(message, []));
    }

    private static ApplicationStageChangedIntegrationEvent StageChangedMessage(
        Guid? candidateAccountId, Guid applicationId) =>
        new(
            applicationId, Guid.NewGuid(), "Staff Engineer",
            Guid.NewGuid(), candidateAccountId, "jane@acme.test", "Jane",
            Guid.NewGuid(), "Applied", Guid.NewGuid(), "Screening", Guid.NewGuid());

    private static InterviewScheduledIntegrationEvent InterviewMessage(
        Guid? candidateAccountId, Guid applicationId, DateTime scheduledAtUtc) =>
        new(
            Guid.NewGuid(), applicationId, Guid.NewGuid(), "Staff Engineer",
            Guid.NewGuid(), candidateAccountId, "jane@acme.test", "Jane",
            "Technical", scheduledAtUtc, 60, "room-token-abc", Guid.NewGuid());

    private static ApplicationViewedIntegrationEvent ViewedMessage(
        Guid? candidateAccountId, Guid applicationId) =>
        new(
            applicationId, Guid.NewGuid(), "Staff Engineer",
            Guid.NewGuid(), candidateAccountId, Guid.NewGuid());

    private static ApplicationCvDownloadedIntegrationEvent CvDownloadedMessage(
        Guid? candidateAccountId, Guid applicationId) =>
        new(
            applicationId, Guid.NewGuid(), "Staff Engineer",
            Guid.NewGuid(), candidateAccountId, Guid.NewGuid());

    private static ApplicationSubmittedIntegrationEvent NewApplicationMessage(
        Guid tenantId, Guid applicationId) =>
        new(
            applicationId, Guid.NewGuid(), "Staff Engineer",
            Guid.NewGuid(), "jane@acme.test", "Jane", "Doe", tenantId);
}
