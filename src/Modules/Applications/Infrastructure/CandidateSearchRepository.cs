using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace Ats.Modules.Applications.Infrastructure;

public sealed class CandidateSearchRepository : ICandidateSearchRepository
{
    private readonly IApplicationsDbContext _db;

    public CandidateSearchRepository(IApplicationsDbContext db) => _db = db;

    public async Task<PagedResult<CandidateSearchResultDto>> SearchAsync(
        string q, int page, int pageSize, CancellationToken ct = default)
    {
        // The global query filter on Candidates already scopes to the current tenant
        // and excludes soft-deleted records — no manual WHERE needed for those.
        // websearch_to_tsquery handles arbitrary user text safely: special characters
        // are ignored rather than causing an exception, and the result is a valid tsquery.
        var candidates = _db.Candidates
            .AsNoTracking()
            .Where(c => EF.Property<NpgsqlTsVector>(c, "SearchVector")
                .Matches(EF.Functions.WebSearchToTsQuery("english", q)));

        var totalCount = await candidates.CountAsync(ct);

        var items = await candidates
            .OrderByDescending(c => EF.Property<NpgsqlTsVector>(c, "SearchVector")
                .Rank(EF.Functions.WebSearchToTsQuery("english", q)))
            .ThenBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CandidateSearchResultDto(
                c.Id, c.FirstName, c.LastName, c.Email, c.Phone, c.LinkedInUrl))
            .ToListAsync(ct);

        return new PagedResult<CandidateSearchResultDto>(items, page, pageSize, totalCount);
    }
}
