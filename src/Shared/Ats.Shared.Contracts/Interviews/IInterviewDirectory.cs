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
}
