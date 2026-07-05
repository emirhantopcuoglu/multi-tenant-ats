namespace Ats.Shared.Contracts.Interviews;

// The Interviews module's public read surface for other parts of the system, mirroring IJobDirectory
// and IApplicationDirectory. This is the first time the Interviews module is read from the outside:
// the dashboard needs an upcoming-interview count, so the port is born with exactly that one method
// and will grow as further needs arise.
//
// Tenant scoping is implicit: the implementation runs inside the resolved tenant's context, so the
// global query filter already restricts the count to the current tenant.
public interface IInterviewDirectory
{
    // Number of still-scheduled interviews at or after the given instant — feeds the dashboard
    // "Upcoming interviews" stat. The caller passes "now" so the boundary stays explicit and testable.
    Task<int> CountUpcomingInterviewsAsync(DateTime nowUtc, CancellationToken cancellationToken = default);

    // The candidate's own transparent view of interviews scheduled against one of their
    // applications. Called from the Applications module's candidate-detail query, which runs
    // cross-tenant (the candidate account is the scope root, not a tenant) and has no ambient
    // tenant to rely on — tenantId is passed explicitly and the lookup bypasses the global filter,
    // the same reasoning as IJobDirectory.GetJobRequirementsAsync.
    Task<IReadOnlyList<CandidateInterviewInfo>> GetForApplicationAsync(
        Guid tenantId, Guid applicationId, CancellationToken cancellationToken = default);
}

// Candidate-safe by shape: no interviewer ids, no recruiter notes. A mapping bug can't leak either
// one because there is no field here to carry them.
public sealed record CandidateInterviewInfo(
    Guid Id,
    string Type,
    DateTime ScheduledAtUtc,
    int DurationMinutes,
    string? Location,
    string Status);
