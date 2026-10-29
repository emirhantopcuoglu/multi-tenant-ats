using Ats.Modules.Notifications.Domain;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Contracts.Tenants;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

// Writes a row for every member of the tenant when someone applies. Fan-out on write is cheaper
// than working out recipients on every poll. Members come from ITenantDirectory.
public sealed class NewApplicationNotificationConsumer : IConsumer<ApplicationSubmittedIntegrationEvent>
{
    private readonly NotificationsDbContext _db;
    private readonly ITenantDirectory _tenantDirectory;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly ILogger<NewApplicationNotificationConsumer> _logger;

    public NewApplicationNotificationConsumer(
        NotificationsDbContext db,
        ITenantDirectory tenantDirectory,
        IIdempotencyGuard idempotencyGuard,
        ILogger<NewApplicationNotificationConsumer> logger)
    {
        _db = db;
        _tenantDirectory = tenantDirectory;
        _idempotencyGuard = idempotencyGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ApplicationSubmittedIntegrationEvent> context)
    {
        var message = context.Message;

        var userIds = await _tenantDirectory.GetTenantUserIdsAsync(message.TenantId, context.CancellationToken);
        var notifications = BuildNotifications(message, userIds);

        if (notifications.Count == 0)
        {
            _logger.LogDebug(
                "Skipped new-application notification fan-out for application {ApplicationId}: " +
                "tenant {TenantId} has no users",
                message.ApplicationId, message.TenantId);
            return;
        }

        // One key for the whole batch, so a redelivery can't double every row.
        var key = $"notifications:in-app:new-application:{context.MessageId}";
        var written = await _idempotencyGuard.ProcessOnceAsync(key, async () =>
        {
            _db.Notifications.AddRange(notifications);
            await _db.SaveChangesAsync(context.CancellationToken);
        });

        if (!written)
        {
            _logger.LogInformation(
                "Skipped duplicate new-application notification fan-out for application {ApplicationId}",
                message.ApplicationId);
            return;
        }

        _logger.LogInformation(
            "Wrote {Count} new-application notifications for application {ApplicationId} in tenant {TenantId}",
            notifications.Count, message.ApplicationId, message.TenantId);
    }

    // Recipient ids are passed in so tests don't need a fake ITenantDirectory.
    public static IReadOnlyList<Notification> BuildNotifications(
        ApplicationSubmittedIntegrationEvent message, IReadOnlyCollection<Guid> tenantUserIds)
    {
        if (tenantUserIds.Count == 0)
            return [];

        var payload = NotificationPayloads.Serialize(new NotificationPayloads.NewApplication(
            message.ApplicationId, message.JobTitle, message.CandidateFirstName, message.CandidateLastName));

        return tenantUserIds
            .Select(userId => Notification.ForCompanyUser(
                message.TenantId, userId, NotificationType.NewApplication, payload))
            .ToList();
    }
}
