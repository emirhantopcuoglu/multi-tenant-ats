using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Notifications.Infrastructure;

// Only read ones: unread notifications have no retention period yet.
public sealed class NotificationRetentionCleaner : IRetentionCleaner
{
    private readonly NotificationsDbContext _db;

    public NotificationRetentionCleaner(NotificationsDbContext db) => _db = db;

    public Task<int> CleanAsync(RetentionCutoffs cutoffs, CancellationToken cancellationToken = default)
    {
        var readBefore = cutoffs.ReadNotificationBeforeUtc;
        return _db.Notifications.Where(n => n.ReadAtUtc < readBefore).ExecuteDeleteAsync(cancellationToken);
    }
}
