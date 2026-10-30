using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Applications.Application.Applications;

// Produced asynchronously, so it may not exist yet for a new application.
public sealed record CvParseResultDto(
    Guid ApplicationId,
    IReadOnlyList<string> Skills,
    double TotalExperienceYears,
    IReadOnlyList<CvEducation> Education,
    IReadOnlyList<CvPosition> RecentPositions,
    CvJobFitRating JobFitRating,
    string FitSummary,
    IReadOnlyList<string> MatchedRequirements,
    IReadOnlyList<string> MissingRequirements,
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
        // Check the application exists in this tenant first, so an unknown id is a 404.
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
            result.JobFitRating,
            result.FitSummary,
            result.MatchedRequirements,
            result.MissingRequirements,
            stored.ParsedAtUtc));
    }
}
