using Ats.Modules.Applications.Domain;
using Microsoft.EntityFrameworkCore;

// The aggregate is named Application, but so is this layer's namespace (...Application). The
// alias lets us name the type unambiguously; without it the compiler reads "Application" as
// the namespace.
using ApplicationEntity = Ats.Modules.Applications.Domain.Application;

namespace Ats.Modules.Applications.Application;

// Application-layer abstraction over the module's persistence. Handlers depend on this, not on
// the concrete ApplicationsDbContext in Infrastructure, keeping the dependency direction
// Infrastructure -> Application and the handlers unit-testable with a fake context.
public interface IApplicationsDbContext
{
    DbSet<ApplicationEntity> Applications { get; }
    DbSet<Candidate> Candidates { get; }
    DbSet<Pipeline> Pipelines { get; }
    DbSet<PipelineStage> PipelineStages { get; }
    DbSet<ApplicationActivity> Activities { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
