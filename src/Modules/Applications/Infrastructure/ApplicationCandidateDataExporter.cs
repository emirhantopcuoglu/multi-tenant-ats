using Ats.Shared.Contracts.Jobs;
using Ats.Shared.Contracts.Tenants;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Applications.Infrastructure;

// Every application of the candidate, in every company, with what they sent and how it ended.
public sealed class ApplicationCandidateDataExporter : ICandidateDataExporter
{
    private readonly ApplicationsDbContext _db;
    private readonly IJobDirectory _jobs;
    private readonly ITenantDirectory _tenants;

    public ApplicationCandidateDataExporter(ApplicationsDbContext db, IJobDirectory jobs, ITenantDirectory tenants)
    {
        _db = db;
        _jobs = jobs;
        _tenants = tenants;
    }

    public string Section => "applications";

    public async Task<object> ExportAsync(Guid candidateAccountId, CancellationToken cancellationToken = default)
    {
        // Across tenants and withdrawn or deleted ones too: they are all still kept.
        var rows = await _db.Applications
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.CandidateAccountId == candidateAccountId)
            .Join(_db.Candidates.IgnoreQueryFilters(), a => a.CandidateId, c => c.Id, (a, c) => new
            {
                a.Id,
                a.JobId,
                a.TenantId,
                a.AppliedAtUtc,
                Status = a.Status.ToString(),
                a.CoverLetter,
                a.RejectionReason,
                a.ClosedAtUtc,
                c.Phone,
                c.LinkedInUrl
            })
            .OrderBy(a => a.AppliedAtUtc)
            .ToListAsync(cancellationToken);

        var jobs = await _jobs.GetSummariesAsync(rows.Select(r => r.JobId).Distinct().ToList(), cancellationToken);
        var companies = await _tenants.GetSummariesAsync(rows.Select(r => r.TenantId).Distinct().ToList(), cancellationToken);

        return rows.Select(r => new
        {
            r.Id,
            JobTitle = jobs.TryGetValue(r.JobId, out var job) ? job.Title : null,
            CompanyName = companies.TryGetValue(r.TenantId, out var company) ? company.CompanyName : null,
            r.AppliedAtUtc,
            r.Status,
            r.CoverLetter,
            r.RejectionReason,
            r.ClosedAtUtc,
            r.Phone,
            r.LinkedInUrl
        }).ToList();
    }
}
