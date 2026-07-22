using Ats.Modules.Notifications.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Notifications.Application.Notifications;

// ---- Mark one read ----
// Ownership is folded into the WHERE, same as the candidate application detail: a notification
// that exists but is addressed to someone else is indistinguishable from one that does not exist,
// so ids cannot be probed.
public sealed record MarkNotificationReadCommand(
    NotificationRecipientType RecipientType,
    Guid RecipientId,
    Guid NotificationId) : ICommand<bool>;

public sealed class MarkNotificationReadHandler : ICommandHandler<MarkNotificationReadCommand, bool>
{
    private readonly INotificationsDbContext _db;

    public MarkNotificationReadHandler(INotificationsDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(MarkNotificationReadCommand command, CancellationToken ct)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(
                n => n.Id == command.NotificationId
                     && n.RecipientType == command.RecipientType
                     && n.RecipientId == command.RecipientId,
                ct);

        if (notification is null)
            return Result.Failure<bool>(NotificationErrors.NotFound);

        // MarkRead is idempotent, so re-marking an already-read row is a harmless no-op — the
        // command never fails for being repeated.
        notification.MarkRead();
        await _db.SaveChangesAsync(ct);

        return Result.Success(true);
    }
}

// ---- Mark all read ----
// One set-based UPDATE instead of load-modify-save per row: the whole point of the "mark all"
// button is that the feed may hold many unread rows, and O(1) statements beat O(n) round trips.
// ExecuteUpdate bypasses the change tracker, which is fine here — the entity has no interceptors
// or domain events to fire on read-marking.
public sealed record MarkAllNotificationsReadCommand(
    NotificationRecipientType RecipientType,
    Guid RecipientId) : ICommand<int>;

public sealed class MarkAllNotificationsReadHandler
    : ICommandHandler<MarkAllNotificationsReadCommand, int>
{
    private readonly INotificationsDbContext _db;

    public MarkAllNotificationsReadHandler(INotificationsDbContext db) => _db = db;

    public async Task<Result<int>> Handle(MarkAllNotificationsReadCommand command, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var updated = await _db.Notifications
            .Where(n => n.RecipientType == command.RecipientType
                        && n.RecipientId == command.RecipientId
                        && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ReadAtUtc, now), ct);

        return Result.Success(updated);
    }
}
