using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Applications.Application.Applications;

// Type travels with each stage so clients can tell working stages from terminal ones
// (FinalHired/FinalRejected) — the move-stage UI must not offer terminal stages as targets.
public sealed record PipelineStageDto(Guid Id, string Name, int Order, string Type);

// Stages of a job's pipeline, ordered for display. Pipelines are per-job (one default pipeline per
// job), so the job id selects exactly one pipeline. An unknown or not-yet-created pipeline yields an
// empty list rather than a 404 — the caller (stage filter / Kanban columns) treats "no stages" the
// same as "no pipeline". Tenant isolation is automatic via the global query filter.
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
