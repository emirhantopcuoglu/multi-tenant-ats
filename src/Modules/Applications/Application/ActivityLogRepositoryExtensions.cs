using Ats.Modules.Applications.Domain;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Applications.Application;

public static class ActivityLogRepositoryExtensions
{
    // The activity log is in MongoDB, outside the Postgres transaction. It is written after the
    // change is saved, and a failure is logged as a warning without undoing the change.
    public static Task TryAddAsync(
        this IActivityLogRepository repository,
        ApplicationActivity activity,
        ILogger logger,
        CancellationToken cancellationToken) =>
        TryAsync(() => repository.AddAsync(activity, cancellationToken), activity, logger);

    // For callers with no ambient tenant, such as message consumers.
    public static Task TryAddAsync(
        this IActivityLogRepository repository,
        ApplicationActivity activity,
        Guid tenantId,
        ILogger logger,
        CancellationToken cancellationToken) =>
        TryAsync(() => repository.AddAsync(activity, tenantId, cancellationToken), activity, logger);

    private static async Task TryAsync(
        Func<Task> write, ApplicationActivity activity, ILogger logger)
    {
        try
        {
            await write();
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to write {ActivityType} activity for application {ApplicationId} to the activity log.",
                activity.ActivityType,
                activity.ApplicationId);
        }
    }
}
