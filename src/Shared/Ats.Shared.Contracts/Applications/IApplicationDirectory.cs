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

    // Number of applications submitted at or after the given instant — feeds the dashboard "New
    // applications this week" stat. The caller decides the window (e.g. the last 7 days) and passes
    // the boundary, so this stays a simple, reusable count.
    Task<int> CountApplicationsSinceAsync(
        DateTime sinceUtc, CancellationToken cancellationToken = default);

    // Number of distinct candidates with at least one Active application — the dashboard "Active
    // candidates" stat. Distinct because one candidate may have several open applications.
    Task<int> CountActiveCandidatesAsync(CancellationToken cancellationToken = default);
}

// The read model scheduling needs. IsActive collapses the Applications module's ApplicationStatus
// into a single question — "can this application still have interviews scheduled?" — so the status
// enum never leaks across the module boundary. The candidate contact and job title are here so the
// Interviews module can publish a self-contained InterviewScheduledIntegrationEvent without ever
// touching the Applications or Jobs schemas. JobTitle falls back to an empty string when the job
// is gone; consumers already treat that as "the role you applied for". CandidateAccountId is the
// global marketplace account behind the application — where an in-app notification is routed —
// and is null for applications submitted before candidate accounts existed. Returns null when no
// such application exists in the current tenant.
public sealed record ApplicationForScheduling(
    Guid Id,
    bool IsActive,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    Guid? CandidateAccountId,
    string CandidateEmail,
    string CandidateFirstName);
