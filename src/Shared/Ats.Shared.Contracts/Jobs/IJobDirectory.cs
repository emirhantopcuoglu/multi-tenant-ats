namespace Ats.Shared.Contracts.Jobs;

// Read port into the Jobs module. Runs in the current tenant.
public interface IJobDirectory
{
    Task<PublishedJob?> GetPublishedJobBySlugAsync(string slug, CancellationToken cancellationToken = default);

    // Any status: the job of a rejected application may already be closed or archived.
    Task<string?> GetJobTitleByIdAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<int> CountOpenJobsAsync(CancellationToken cancellationToken = default);

    // Spans tenants, for the candidate portal. Missing ids are left out.
    Task<IReadOnlyDictionary<Guid, JobSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> jobIds, CancellationToken cancellationToken = default);

    // For the CV parser, which has no ambient tenant; the tenant is passed explicitly.
    Task<JobRequirements?> GetJobRequirementsAsync(
        Guid tenantId, Guid jobId, CancellationToken cancellationToken = default);
}

// The description is the only requirements source a job has.
public sealed record JobRequirements(string Title, string Description);

public sealed record PublishedJob(Guid Id, string Title, string Slug);

// TenantId lets the caller batch the company-name lookup.
public sealed record JobSummary(Guid Id, string Title, string Slug, Guid TenantId);
