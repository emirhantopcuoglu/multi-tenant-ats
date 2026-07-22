namespace Ats.Shared.Contracts.Jobs;

// The Jobs module's public read surface for other modules. Modules never reference each
// other directly (a data leak and a coupling risk); they talk through ports like this one,
// which lives in a neutral shared assembly both sides can see. The Applications module uses
// it to confirm a job exists and is open before accepting an application, without ever
// touching the Jobs schema or its EF model.
//
// Tenant scoping is implicit: the implementation runs inside the resolved tenant's context,
// so the global query filter already restricts the lookup to the current tenant.
public interface IJobDirectory
{
    Task<PublishedJob?> GetPublishedJobBySlugAsync(string slug, CancellationToken cancellationToken = default);

    // Looks up a job's title by id regardless of its status. The Applications module needs this to
    // name the job in a rejection email, where the job may already be Closed or Archived — so unlike
    // the slug lookup above, this one is not restricted to Published. Returns null if no such job
    // exists in the current tenant. The port grows one method at a time, as real needs arise.
    Task<string?> GetJobTitleByIdAsync(Guid jobId, CancellationToken cancellationToken = default);

    // Number of currently open (Published) jobs in the tenant. Feeds the dashboard "Open jobs" stat;
    // the count is computed in the Jobs module so its status semantics never leak across the boundary.
    Task<int> CountOpenJobsAsync(CancellationToken cancellationToken = default);

    // Cross-tenant batch lookup for the candidate portal. A candidate's applications span multiple
    // tenants, so this bypasses the global tenant filter. Named after GetSummariesAsync on
    // ITenantDirectory to be consistent with the pattern already used for company name lookups.
    // Missing ids are absent from the result; the caller decides how to handle a gap.
    Task<IReadOnlyDictionary<Guid, JobSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> jobIds, CancellationToken cancellationToken = default);

    // The CV-parsing consumer needs a job's requirements to judge candidate fit, and — like the
    // notification fan-out consumer — runs outside a resolved-tenant request, so tenantId is passed
    // explicitly and the lookup bypasses the ambient-tenant global filter rather than relying on it.
    Task<JobRequirements?> GetJobRequirementsAsync(
        Guid tenantId, Guid jobId, CancellationToken cancellationToken = default);
}

// What a CV needs to be judged against: the job's title (context) and its free-text description
// (the only requirements source the Jobs module has today — there is no separate structured
// "required skills" list).
public sealed record JobRequirements(string Title, string Description);

// A minimal read model — only what a consumer needs to attach an application to a job. It is
// deliberately not the Job entity: exposing the aggregate would leak the Jobs module's
// internals and let other modules depend on its shape.
public sealed record PublishedJob(Guid Id, string Title, string Slug);

// Display info for a single job, used by the cross-tenant candidate portal view.
// TenantId is included so the handler can batch the company-name lookup against ITenantDirectory.
public sealed record JobSummary(Guid Id, string Title, string Slug, Guid TenantId);
