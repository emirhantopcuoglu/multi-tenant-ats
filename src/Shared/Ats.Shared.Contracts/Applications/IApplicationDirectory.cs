namespace Ats.Shared.Contracts.Applications;

// The Applications module's public read surface for other modules, mirroring IJobDirectory. The
// Interviews module uses it to confirm an application exists and is still open before scheduling an
// interview against it — without ever referencing the Applications module or touching its schema.
//
// Tenant scoping is implicit: the implementation runs inside the resolved tenant's context, so the
// global query filter already restricts the lookup to the current tenant. An application from
// another tenant simply looks like it does not exist.
public interface IApplicationDirectory
{
    Task<ApplicationForScheduling?> GetForSchedulingAsync(
        Guid applicationId, CancellationToken cancellationToken = default);

    // Resolves candidate display names for a set of applications. The interview list holds only
    // application ids; this lets it show the candidate without the Interviews module knowing the
    // Applications schema. Ids with no match (e.g. another tenant's) are simply absent from the map.
    Task<IReadOnlyDictionary<Guid, string>> GetCandidateNamesByApplicationAsync(
        IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken = default);
}

// A minimal read model: only what scheduling needs. IsActive collapses the Applications module's
// ApplicationStatus into a single question — "can this application still have interviews scheduled?"
// — so the status enum never leaks across the module boundary. Returns null when no such application
// exists in the current tenant.
public sealed record ApplicationForScheduling(Guid Id, bool IsActive);
