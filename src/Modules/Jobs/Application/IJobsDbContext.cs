using Ats.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Jobs.Application;

// Abstraction owned by the Application layer so handlers depend on behaviour, not on the
// concrete JobsDbContext in Infrastructure. This keeps the dependency direction
// Infrastructure -> Application (Clean Architecture) and avoids a circular project reference.
public interface IJobsDbContext
{
    DbSet<Job> Jobs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
