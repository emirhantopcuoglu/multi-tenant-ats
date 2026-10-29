using Ats.Modules.Applications.Domain;

namespace Ats.Modules.Applications.Application;

// Stored in MongoDB. Writes take the domain model, reads return a flat projection.
public interface IActivityLogRepository
{
    Task AddAsync(ApplicationActivity activity, CancellationToken cancellationToken = default);

    // For consumers, which have no ambient tenant; the overload above would throw there.
    Task AddAsync(
        ApplicationActivity activity, Guid tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActivityLogEntry>> GetByApplicationAsync(
        Guid applicationId, CancellationToken cancellationToken = default);

    // For candidate requests, which have no tenant. Pass the tenant from a verified application row,
    // never one supplied by the caller.
    Task<IReadOnlyList<ActivityLogEntry>> GetByApplicationAsync(
        Guid applicationId, Guid tenantId, CancellationToken cancellationToken = default);
}

// Payload is raw JSON; the API turns it into a JSON object.
public sealed record ActivityLogEntry(
    Guid Id,
    Guid ApplicationId,
    string ActivityType,
    Guid? ActorUserId,
    string Payload,
    DateTime OccurredAtUtc);
