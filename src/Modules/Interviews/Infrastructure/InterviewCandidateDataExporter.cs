using Ats.Shared.Contracts.Applications;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Infrastructure;

// When the candidate was interviewed and how it went for them. The interviewers' notes and feedback are
// the company's own assessment and stay out.
public sealed class InterviewCandidateDataExporter : ICandidateDataExporter
{
    private readonly InterviewsDbContext _db;
    private readonly IApplicationDirectory _applications;

    public InterviewCandidateDataExporter(InterviewsDbContext db, IApplicationDirectory applications)
    {
        _db = db;
        _applications = applications;
    }

    public string Section => "interviews";

    public async Task<object> ExportAsync(Guid candidateAccountId, CancellationToken cancellationToken = default)
    {
        var applicationIds = await _applications.GetApplicationIdsForCandidateAccountAsync(
            candidateAccountId, cancellationToken);

        return await _db.Interviews
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(i => applicationIds.Contains(i.ApplicationId))
            .OrderBy(i => i.ScheduledAtUtc)
            .Select(i => new
            {
                i.Id,
                i.ApplicationId,
                Type = i.Type.ToString(),
                i.ScheduledAtUtc,
                i.DurationMinutes,
                Status = i.Status.ToString(),
                CancellationReason = i.CancellationReason.HasValue ? i.CancellationReason.Value.ToString() : null
            })
            .ToListAsync(cancellationToken);
    }
}
