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
        Assert.Equal("Google Meet", payload.RootElement.GetProperty("location").GetString());
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
            "Technical", scheduledAtUtc, 60, "Google Meet", Guid.NewGuid());
}
