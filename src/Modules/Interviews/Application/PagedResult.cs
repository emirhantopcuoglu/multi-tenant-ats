namespace Ats.Modules.Interviews.Application;

// Same shape as the other modules' PagedResult. Kept per-module rather than shared in the kernel so
// each module owns its application contracts; promote it only if it becomes painful (Rule of Three).
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
