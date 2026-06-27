using Ats.Shared.Kernel;
using FluentValidation;

namespace Ats.Modules.Applications.Application.Applications;

public sealed record CandidateSearchResultDto(
    Guid Id, string FirstName, string LastName, string Email, string? Phone, string? LinkedInUrl);

// Port: Application declares what it needs; Infrastructure wires the PostgreSQL FTS implementation.
// Keeping this here (not in Shared.Kernel) follows the pattern of IActivityLogRepository and
// ICvParseResultRepository: each module owns its own persistence ports.
public interface ICandidateSearchRepository
{
    Task<PagedResult<CandidateSearchResultDto>> SearchAsync(
        string q, int page, int pageSize, CancellationToken ct = default);
}

public sealed record SearchCandidatesQuery(string Q, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<CandidateSearchResultDto>>;

public sealed class SearchCandidatesValidator : AbstractValidator<SearchCandidatesQuery>
{
    public SearchCandidatesValidator()
    {
        RuleFor(x => x.Q).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class SearchCandidatesHandler
    : IQueryHandler<SearchCandidatesQuery, PagedResult<CandidateSearchResultDto>>
{
    private readonly ICandidateSearchRepository _repository;

    public SearchCandidatesHandler(ICandidateSearchRepository repository)
        => _repository = repository;

    public async Task<Result<PagedResult<CandidateSearchResultDto>>> Handle(
        SearchCandidatesQuery query, CancellationToken ct)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;
        var result = await _repository.SearchAsync(query.Q, page, pageSize, ct);
        return Result.Success(result);
    }
}
