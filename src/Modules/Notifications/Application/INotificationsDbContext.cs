using Ats.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Notifications.Application;

public interface INotificationsDbContext
{
    DbSet<Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
