using Ats.Modules.Interviews.Domain;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Infrastructure;

// Clears what the companies wrote about a deleted candidate in their interviews. Ratings, outcomes and
// times stay. Runs with no tenant, so the query filters are bypassed and the audit interceptor stays out.
public sealed class InterviewCandidateDataEraser : ICandidateDataEraser
{
    private static readonly string InterviewResourceType = typeof(Interview).Name;
    private static readonly string FeedbackResourceType = typeof(InterviewFeedback).Name;

    private readonly InterviewsDbContext _db;
    private readonly IApplicationDirectory _applications;

    public InterviewCandidateDataEraser(InterviewsDbContext db, IApplicationDirectory applications)
    {
        _db = db;
        _applications = applications;
    }

    // Updates in place rather than load and save: the withdrawal just before this cancels the same
    // interviews, and a save from a loaded copy would fail on the row version.
    public async Task EraseAsync(Guid candidateAccountId, CancellationToken cancellationToken = default)
    {
        var applicationIds = await _applications.GetApplicationIdsForCandidateAccountAsync(
            candidateAccountId, cancellationToken);
        if (applicationIds.Count == 0)
            return;

        // Deleted interviews too: they still hold the notes.
        var interviews = _db.Interviews.IgnoreQueryFilters().Where(i => applicationIds.Contains(i.ApplicationId));
        var feedback = _db.Feedback.IgnoreQueryFilters().Where(f => interviews.Any(i => i.Id == f.InterviewId));
        var interviewIds = await interviews.Select(i => i.Id.ToString()).ToListAsync(cancellationToken);
        var feedbackIds = await feedback.Select(f => f.Id.ToString()).ToListAsync(cancellationToken);

        await interviews.ExecuteUpdateAsync(
            set => set.SetProperty(i => i.Notes, (string?)null).SetProperty(i => i.CancellationNote, (string?)null),
            cancellationToken);
        await feedback.ExecuteUpdateAsync(set => set.SetProperty(f => f.Comments, (string?)null), cancellationToken);

        await _db.Set<AuditLogEntry>()
            .Where(e => (e.ResourceType == InterviewResourceType && interviewIds.Contains(e.ResourceId))
                        || (e.ResourceType == FeedbackResourceType && feedbackIds.Contains(e.ResourceId)))
            .ExecuteUpdateAsync(
                set => set.SetProperty(e => e.Before, (string?)null).SetProperty(e => e.After, (string?)null),
                cancellationToken);
    }
}
