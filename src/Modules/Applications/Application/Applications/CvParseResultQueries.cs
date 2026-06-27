using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Applications.Application.Applications;

// Read side of CV parsing (Sprint 6.3): exposes the structured result a recruiter can view on the
// application. The result is produced asynchronously by the CV-parsing consumer and stored in
// MongoDB, so it may not exist yet for a freshly submitted application.
public sealed record CvParseResultDto(
    Guid ApplicationId,
    IReadOnlyList<string> Skills,
    double TotalExperienceYears,
    IReadOnlyList<CvEducation> Education,
    IReadOnlyList<CvPosition> RecentPositions,
    DateTime ParsedAtUtc);

public sealed record GetCvParseResultQuery(Guid ApplicationId) : IQuery<CvParseResultDto>;

public sealed class GetCvParseResultHandler : IQueryHandler<GetCvParseResultQuery, CvParseResultDto>
{
    private readonly IApplicationsDbContext _db;
    private readonly ICvParseResultRepository _repository;

    public GetCvParseResultHandler(IApplicationsDbContext db, ICvParseResultRepository repository)
    {
        _db = db;
        _repository = repository;
    }

    public async Task<Result<CvParseResultDto>> Handle(GetCvParseResultQuery query, CancellationToken ct)
    {
        // Confirm the application exists in this tenant first (the EF global filter scopes this), so
        // an unknown id yields a clean 404 rather than leaking whether some other tenant's
        // application has a parse result.
        var exists = await _db.Applications.AsNoTracking()
            .AnyAsync(a => a.Id == query.ApplicationId, ct);
        if (!exists)
            return Result.Failure<CvParseResultDto>(ApplicationErrors.NotFound);

        var stored = await _repository.GetByApplicationAsync(query.ApplicationId, ct);
        if (stored is null)
            return Result.Failure<CvParseResultDto>(ApplicationErrors.CvNotParsed);

        var result = stored.Result;
        return Result.Success(new CvParseResultDto(
            stored.ApplicationId,
            result.Skills,
            result.TotalExperienceYears,
            result.Education,
            result.RecentPositions,
            stored.ParsedAtUtc));
    }
}
