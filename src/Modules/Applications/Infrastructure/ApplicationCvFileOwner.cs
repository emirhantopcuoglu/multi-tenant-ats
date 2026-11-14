using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Applications.Infrastructure;

// The CV each application was sent with.
public sealed class ApplicationCvFileOwner : IStoredFileOwner
{
    private readonly ApplicationsDbContext _db;

    public ApplicationCvFileOwner(ApplicationsDbContext db) => _db = db;

    // IgnoreQueryFilters: the cleanup has no tenant, which would hide every application, and a
    // soft-deleted application still keeps its CV.
    public async Task<IReadOnlyCollection<string>> FindReferencedAsync(
        IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
        await _db.Applications
            .IgnoreQueryFilters()
            .Where(application => application.CvFileKey != null && keys.Contains(application.CvFileKey))
            .Select(application => application.CvFileKey!)
            .ToListAsync(cancellationToken);
}
