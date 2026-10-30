using Ats.Modules.Applications.Domain;
using Microsoft.EntityFrameworkCore;

// Alias because "Application" is also this namespace.
using ApplicationEntity = Ats.Modules.Applications.Domain.Application;

namespace Ats.Modules.Applications.Application;

public interface IApplicationsDbContext
{
    DbSet<ApplicationEntity> Applications { get; }
    DbSet<Candidate> Candidates { get; }
    DbSet<Pipeline> Pipelines { get; }
    DbSet<PipelineStage> PipelineStages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
