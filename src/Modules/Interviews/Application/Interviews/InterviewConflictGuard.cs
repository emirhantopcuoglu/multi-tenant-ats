using Ats.Modules.Interviews.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Application.Interviews;

// Rejects a schedule/reschedule that would put an interviewer — or the candidate — in two
// overlapping interviews at once. Shared by ScheduleInterviewHandler and RescheduleInterviewHandler,
// a static helper in the same spirit as InterviewTransition.ApplyAsync. Half-open intervals, so a
// back-to-back pair (one ending exactly as the next starts) is not an overlap; an equal start, or
// any real intersection, is.
internal static class InterviewConflictGuard
{
    // Only used to bound the query cheaply. New interviews are capped at 60 min
    // (Interview.AllowedDurationMinutes), but rows created before that rule could be longer, so an
    // 8-hour lookback keeps the overlap check correct for any historical duration.
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

        // One tenant-scoped query (the ambient global filter applies) for the small set of still-
        // scheduled interviews whose start could possibly overlap the proposed window. The precise
        // overlap is finished in memory because the end time is start + a per-row duration, awkward
        // to express in SQL; the candidate set is tiny, so materializing it is cheap.
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

        // Interviewer first, matching the recruiter's mental model (a person can't be in two rooms),
        // then the candidate.
        if (overlapping.Any(i => i.InterviewerUserIds.Any(interviewerUserIds.Contains)))
            return InterviewErrors.InterviewerConflict;

        if (overlapping.Any(i => candidateApplicationIds.Contains(i.ApplicationId)))
            return InterviewErrors.CandidateConflict;

        return null;
    }

    private static bool Overlaps(DateTime startA, DateTime endA, DateTime startB, DateTime endB) =>
        startA < endB && startB < endA;
}
