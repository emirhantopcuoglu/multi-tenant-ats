using Ats.Modules.Interviews.Application.Events;
using Ats.Modules.Interviews.Domain;
using Ats.Shared.Contracts.Applications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Interviews.Infrastructure;

// Delivers the reminders an upcoming interview owes. Scheduled by Hangfire from the composition
// root; like ExpiredInvitationCleanupJob it has no Hangfire dependency of its own, so it stays a
// plain class the tests can call directly.
//
// A periodic sweep rather than one delayed job per interview. The alternative would mean storing a
// Hangfire job id on the interview row and cancelling/re-creating it on every reschedule and
// cancellation — two systems holding the same truth, where a lost id is a reminder that silently
// never arrives. A sweep re-reads the current state every run, so rescheduling and cancelling need
// no scheduler-side handling at all: the domain already moved the due instant, or cleared it.
public sealed class InterviewReminderJob
{
    // Upper bound on one run. Reminders come due at a rate set by the interview schedule, not by
    // this number, so a backlog only forms after an outage — and then it drains over the following
    // runs instead of one sweep holding a transaction open across thousands of rows.
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
        // Read once and passed down, so every decision in this run agrees on what "now" is.
        var now = DateTime.UtcNow;

        var due = await LoadDueInterviewsAsync(now, ct);
        if (due.Count == 0)
            return;

        // One batched lookup for the whole sweep. Resolving the candidate and job behind each
        // interview individually would be an N+1: the cost of a run would scale with the number of
        // reminders rather than with the single query the work actually needs.
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

        // Every reminder examined above is settled by this single save. A publish that throws takes
        // the whole run down before it — nothing is cleared, Hangfire retries, and the reminders
        // that did go out are republished into consumers that deduplicate them.
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Interview reminder sweep settled {SettledCount} due reminder(s), {PublishedCount} published",
            due.Count, published);
    }

    // IgnoreQueryFilters because a background job has no ambient tenant for the global filter to
    // match — the sweep is system-wide by definition, the same reason ExpiredInvitationCleanupJob
    // bypasses it. That bypass also drops the soft-delete filter, so IsDeleted is restated by hand:
    // a deleted interview must not reach anyone's inbox.
    private Task<List<Interview>> LoadDueInterviewsAsync(DateTime nowUtc, CancellationToken ct) =>
        _db.Interviews
            .IgnoreQueryFilters()
            .Where(interview => !interview.IsDeleted
                && (interview.DayBeforeReminderDueAtUtc <= nowUtc
                    || interview.StartingSoonReminderDueAtUtc <= nowUtc))
            .OrderBy(interview => interview.ScheduledAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

    // A null due instant compares false, so this yields only what is actually owed.
    private static IEnumerable<InterviewReminderKind> DueKinds(Interview interview, DateTime nowUtc)
    {
        if (interview.DayBeforeReminderDueAtUtc <= nowUtc)
            yield return InterviewReminderKind.DayBefore;

        if (interview.StartingSoonReminderDueAtUtc <= nowUtc)
            yield return InterviewReminderKind.StartingSoon;
    }

    // Returns whether anything was published. Every other outcome still clears the reminder: none of
    // them can become deliverable later, so leaving the row pending would only have the sweep
    // re-examine a dead interview on every run from now on.
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
            // The application was hard-deleted out from under the interview. Worth a warning rather
            // than a silent skip: there is no legitimate path that produces this state.
            _logger.LogWarning(
                "Dropped {ReminderKind} reminder for interview {InterviewId}: application {ApplicationId} no longer exists",
                kind, interview.Id, interview.ApplicationId);
            interview.ClearReminder(kind);
            return false;
        }

        // Published before the reminder is cleared, and the order matters. If the save fails, the
        // next sweep republishes and the consumers — keyed on (interview, kind), not on a message id
        // — drop the repeat. Clearing first would instead lose the reminder outright, which is the
        // failure a candidate actually notices.
        await _publisher.Publish(BuildEvent(interview, application, kind), ct);
        interview.ClearReminder(kind);
        return true;
    }

    // Both reminders fall due together when the sweep has not run for a while. Only the later one is
    // still true by then: "your interview is tomorrow", delivered ten minutes before it starts,
    // reads as a broken system rather than a courtesy, so the day-before nudge is settled silently.
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
