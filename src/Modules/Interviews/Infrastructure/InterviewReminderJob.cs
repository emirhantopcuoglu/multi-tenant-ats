using Ats.Modules.Interviews.Application.Events;
using Ats.Modules.Interviews.Domain;
using Ats.Shared.Contracts.Applications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Interviews.Infrastructure;

// Sends due interview reminders. Scheduled by Hangfire. A periodic sweep instead of one delayed
// job per interview, so reschedules and cancels need no scheduler changes.
public sealed class InterviewReminderJob
{
    // Caps one run; a backlog after an outage drains over the next runs.
    private const int BatchSize = 200;

    private readonly InterviewsDbContext _db;
    private readonly IApplicationDirectory _applications;
    private readonly IPublisher _publisher;
    private readonly ILogger<InterviewReminderJob> _logger;

    public InterviewReminderJob(
        InterviewsDbContext db,
        IApplicationDirectory applications,
        IPublisher publisher,
        ILogger<InterviewReminderJob> logger)
    {
        _db = db;
        _applications = applications;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task SendDueRemindersAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var due = await LoadDueInterviewsAsync(now, ct);
        if (due.Count == 0)
            return;

        // One batched lookup for the whole run instead of one per interview.
        var applications = await _applications.GetForSchedulingAsync(
            due.Select(interview => interview.ApplicationId).Distinct().ToList(), ct);

        var published = 0;
        foreach (var interview in due)
        {
            foreach (var kind in DueKinds(interview, now))
            {
                if (await TryPublishAsync(interview, kind, applications, now, ct))
                    published++;
            }
        }

        // One save settles every reminder above. If a publish throws, nothing is cleared and Hangfire
        // retries; consumers drop repeats.
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Interview reminder sweep settled {SettledCount} due reminder(s), {PublishedCount} published",
            due.Count, published);
    }

    // No ambient tenant in a job, so the filter is bypassed and IsDeleted is checked by hand.
    private Task<List<Interview>> LoadDueInterviewsAsync(DateTime nowUtc, CancellationToken ct) =>
        _db.Interviews
            .IgnoreQueryFilters()
            .Where(interview => !interview.IsDeleted
                && (interview.DayBeforeReminderDueAtUtc <= nowUtc
                    || interview.StartingSoonReminderDueAtUtc <= nowUtc))
            .OrderBy(interview => interview.ScheduledAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

    private static IEnumerable<InterviewReminderKind> DueKinds(Interview interview, DateTime nowUtc)
    {
        if (interview.DayBeforeReminderDueAtUtc <= nowUtc)
            yield return InterviewReminderKind.DayBefore;

        if (interview.StartingSoonReminderDueAtUtc <= nowUtc)
            yield return InterviewReminderKind.StartingSoon;
    }

    // Returns whether anything was published. The reminder is cleared either way.
    private async Task<bool> TryPublishAsync(
        Interview interview,
        InterviewReminderKind kind,
        IReadOnlyDictionary<Guid, ApplicationForScheduling> applications,
        DateTime nowUtc,
        CancellationToken ct)
    {
        if (IsSuperseded(interview, kind, nowUtc) || !interview.CanRemind(nowUtc))
        {
            interview.ClearReminder(kind);
            return false;
        }

        if (!applications.TryGetValue(interview.ApplicationId, out var application))
        {
            // Shouldn't happen, so log it.
            _logger.LogWarning(
                "Dropped {ReminderKind} reminder for interview {InterviewId}: application {ApplicationId} no longer exists",
                kind, interview.Id, interview.ApplicationId);
            interview.ClearReminder(kind);
            return false;
        }

        // Publish before clearing: a failed save means a repeat (deduped), clearing first could lose it.
        await _publisher.Publish(BuildEvent(interview, application, kind), ct);
        interview.ClearReminder(kind);
        return true;
    }

    // If both are due at once, only send the starting-soon one.
    private static bool IsSuperseded(
        Interview interview, InterviewReminderKind kind, DateTime nowUtc) =>
        kind == InterviewReminderKind.DayBefore
        && interview.StartingSoonReminderDueAtUtc <= nowUtc;

    private static InterviewReminderDueEvent BuildEvent(
        Interview interview, ApplicationForScheduling application, InterviewReminderKind kind) =>
        new(interview.Id,
            application.Id,
            application.JobId,
            application.JobTitle,
            application.CandidateId,
            application.CandidateAccountId,
            application.CandidateEmail,
            application.CandidateFirstName,
            interview.Type,
            interview.ScheduledAtUtc,
            interview.DurationMinutes,
            interview.RoomToken,
            kind,
            interview.TenantId);
}
