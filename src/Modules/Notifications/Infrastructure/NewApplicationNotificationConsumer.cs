using Ats.Modules.Notifications.Domain;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Contracts.Tenants;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

// Writes an in-app notification row for every member of the tenant when a candidate submits a new
// application — the "yeni başvurunuz var" signal from roadmap 3.2, and the first consumer of the
// CompanyUser recipient type. Fan-out on write, decided up front: every tenant user should see new
// applications without a separate "watchers" concept, and rows are cheap compared to the alternative
// of computing "who should see this" at read time on every poll.
//
// The recipient list comes from ITenantDirectory (the Tenants module's cross-module read port) —
// this module has no view onto ApplicationUser, an Identity entity that lives in the Tenants schema.
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

        // One guard key for the whole batch: a redelivery must not double every row, so the fan-out
        // is all-or-nothing, not per-recipient.
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

    // The message-to-rows mapping, kept static and side-effect free (recipient ids passed in rather
    // than resolved here) so tests can pin the payload contents and the fan-out count without a fake
    // ITenantDirectory or a ConsumeContext.
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
