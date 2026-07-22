using Ats.Shared.Kernel;

namespace Ats.Modules.Notifications.Application;

public static class NotificationErrors
{
    public static readonly Error NotFound =
        new("notification.not_found", "Notification not found.");
}
