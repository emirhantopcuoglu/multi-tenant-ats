using Ats.Modules.Interviews.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Application.Interviews;

// Rejects a slot that would double-book an interviewer or the candidate. Intervals are half-open,
// so back-to-back interviews don't conflict.
internal static class InterviewConflictGuard
{
    // Only bounds the query. Older rows may be longer than today's 60-minute cap.
    private static readonly TimeSpan MaxConsideredDuration = TimeSpan.FromHours(8);

    public static async Task<Error?> CheckAsync(
        IInterviewsDbContext db,
        DateTime proposedStartUtc,
        int durationMinutes,
        IReadOnlyCollection<Guid> interviewerUserIds,
        IReadOnlyCollection<Guid> candidateApplicationIds,
        Guid? excludeInterviewId,
        CancellationToken ct)
    {
        var proposedEndUtc = proposedStartUtc.AddMinutes(durationMinutes);
        var windowStart = proposedStartUtc - MaxConsideredDuration;

        // Narrow the candidates in SQL, finish the overlap check in memory (end time is start + duration).
        var scheduled = await db.Interviews
            .AsNoTracking()
            .Where(i => i.Status == InterviewStatus.Scheduled
                        && i.ScheduledAtUtc >= windowStart
                        && i.ScheduledAtUtc < proposedEndUtc
                        && (excludeInterviewId == null || i.Id != excludeInterviewId))
            .ToListAsync(ct);

        var overlapping = scheduled
            .Where(i => Overlaps(
                proposedStartUtc, proposedEndUtc,
                i.ScheduledAtUtc, i.ScheduledAtUtc.AddMinutes(i.DurationMinutes)))
            .ToList();

        if (overlapping.Any(i => i.InterviewerUserIds.Any(interviewerUserIds.Contains)))
            return InterviewErrors.InterviewerConflict;

        if (overlapping.Any(i => candidateApplicationIds.Contains(i.ApplicationId)))
            return InterviewErrors.CandidateConflict;

        return null;
    }

    private static bool Overlaps(DateTime startA, DateTime endA, DateTime startB, DateTime endB) =>
        startA < endB && startB < endA;
}
