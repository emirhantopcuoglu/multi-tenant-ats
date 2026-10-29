using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Applications.Application.Applications;

// Type lets the UI leave terminal stages out of the move targets.
public sealed record PipelineStageDto(Guid Id, string Name, int Order, string Type);

// No pipeline yet means an empty list, not a 404.
public sealed record ListPipelineStagesQuery(Guid JobId) : IQuery<IReadOnlyList<PipelineStageDto>>;

public sealed class ListPipelineStagesHandler
    : IQueryHandler<ListPipelineStagesQuery, IReadOnlyList<PipelineStageDto>>
{
    private readonly IApplicationsDbContext _db;
    public ListPipelineStagesHandler(IApplicationsDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<PipelineStageDto>>> Handle(
        ListPipelineStagesQuery query, CancellationToken ct)
    {
        var stages = await (
            from p in _db.Pipelines.AsNoTracking()
            join s in _db.PipelineStages.AsNoTracking() on p.Id equals s.PipelineId
            where p.JobId == query.JobId
            orderby s.Order
            select new PipelineStageDto(s.Id, s.Name, s.Order, s.Type.ToString()))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<PipelineStageDto>>(stages);
    }
}
