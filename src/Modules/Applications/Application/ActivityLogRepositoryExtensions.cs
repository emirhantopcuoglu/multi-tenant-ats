using Ats.Modules.Applications.Domain;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Applications.Application;

public static class ActivityLogRepositoryExtensions
{
    // The activity log lives in MongoDB, a separate system, so it can no longer share the
    // operation's PostgreSQL transaction. The business state is the source of truth and is
    // committed first; the log entry is a best-effort write afterwards. If it fails we log a
    // warning (with context, never a silent swallow) and let the operation stand — losing one
    // append-only history line must not fail or roll back a stage move that already happened.
    // Durable cross-system delivery (write the log no-matter-what) arrives with the outbox
    // pattern in Sprint 5.
    public static Task TryAddAsync(
        this IActivityLogRepository repository,
        ApplicationActivity activity,
        ILogger logger,
        CancellationToken cancellationToken) =>
        TryAsync(() => repository.AddAsync(activity, cancellationToken), activity, logger);

    // For callers with no ambient tenant (message consumers), which the overload above cannot serve.
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
