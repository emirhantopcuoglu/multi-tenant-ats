using Ats.Modules.Applications.Domain;

namespace Ats.Modules.Applications.Application;

// Port over the activity log's persistence. From Sprint 4 the log lives in MongoDB, but this
// abstraction never mentions Mongo — handlers depend on behaviour, not on the store (the same
// Dependency Inversion as IFileStorage / IApplicationsDbContext).
//
// The write side accepts the domain model (ApplicationActivity, built via its factory methods);
// the read side returns a flat projection (ActivityLogEntry) rather than rehydrating the
// aggregate, because a timeline view has no need for domain behaviour. This read/write asymmetry
// is the CQRS reflex applied at the repository.
public interface IActivityLogRepository
{
    Task AddAsync(ApplicationActivity activity, CancellationToken cancellationToken = default);

    // Same write, tenant passed explicitly. A message consumer has no ambient tenant, so the
    // request-scoped overload above always throws there — silently, since writes are best-effort.
    Task AddAsync(
        ApplicationActivity activity, Guid tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActivityLogEntry>> GetByApplicationAsync(
        Guid applicationId, CancellationToken cancellationToken = default);

    // Same read, but with the tenant passed explicitly instead of taken from the request. A
    // candidate reads their own application's timeline from a tenant-less request, so the
    // caller supplies the tenant from the application row itself — and only after verifying
    // the application belongs to that candidate. Never call this with a caller-supplied tenant.
    Task<IReadOnlyList<ActivityLogEntry>> GetByApplicationAsync(
        Guid applicationId, Guid tenantId, CancellationToken cancellationToken = default);
}

// Read model for one activity entry. Payload is the raw JSON document as a string; the API layer
// turns it into a real JSON object so the HTTP response is not a string-inside-a-string.
public sealed record ActivityLogEntry(
    Guid Id,
    Guid ApplicationId,
    string ActivityType,
    Guid? ActorUserId,
    string Payload,
    DateTime OccurredAtUtc);
