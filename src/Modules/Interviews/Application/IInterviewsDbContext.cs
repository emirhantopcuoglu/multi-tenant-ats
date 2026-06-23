using Ats.Modules.Interviews.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Application;

// Application-layer abstraction over the module's persistence. Handlers depend on this, not on the
// concrete InterviewsDbContext in Infrastructure, keeping the dependency direction
// Infrastructure -> Application and the handlers unit-testable with a fake context.
public interface IInterviewsDbContext
{
    DbSet<Interview> Interviews { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
