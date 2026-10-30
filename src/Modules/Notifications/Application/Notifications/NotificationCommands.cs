using Ats.Modules.Notifications.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Notifications.Application.Notifications;

// Ownership is part of the WHERE, so someone else's id looks like a missing one.
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

        notification.MarkRead();
        await _db.SaveChangesAsync(ct);

        return Result.Success(true);
    }
}

// One set-based UPDATE instead of loading every unread row.
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
