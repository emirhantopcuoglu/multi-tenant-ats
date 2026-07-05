using Ats.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Notifications.Application;

// The persistence port the handlers depend on, mirroring the other modules: the Application layer
// sees an interface, Infrastructure supplies the concrete NotificationsDbContext behind it.
public interface INotificationsDbContext
{
    DbSet<Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
