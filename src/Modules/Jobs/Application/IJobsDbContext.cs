using Ats.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Jobs.Application;

public interface IJobsDbContext
{
    DbSet<Job> Jobs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
