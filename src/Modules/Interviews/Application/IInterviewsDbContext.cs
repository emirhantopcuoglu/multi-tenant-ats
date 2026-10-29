using Ats.Modules.Interviews.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Application;

public interface IInterviewsDbContext
{
    DbSet<Interview> Interviews { get; }
    DbSet<InterviewFeedback> Feedback { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
