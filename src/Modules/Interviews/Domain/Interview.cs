using System.Security.Cryptography;
using Ats.Shared.Kernel;

namespace Ats.Modules.Interviews.Domain;

// A single interview scheduled against one application. Aggregate root. It references the
// application by id only — never the Application object — so the two aggregates stay independent
// (the same discipline as Application referencing its job/candidate by id). The lifecycle
// (Scheduled -> terminal) is enforced here, not in a service.
public sealed class Interview : ITenantScoped, IAuditable, ISoftDeletable
{
    // How long before the scheduled time the room becomes reachable, and how long it stays
    // reachable afterwards. Generous enough to absorb early joiners and a running-over interview
    // without becoming a permanently-open door once the interview is clearly over.
    public const int RoomOpenLeadMinutes = 10;
    public const int RoomCloseGraceMinutes = 15;

    // How far ahead of the start the day-before reminder is owed. A day is the point where the
    // candidate can still act on it — prepare, or ask to move it — which a nudge minutes before
    // cannot offer. The second reminder deliberately has no constant of its own: it is due exactly
    // when the room opens, so the join link it carries works the moment the email lands.
    public const int DayBeforeReminderLeadHours = 24;

    // The shortest notice an interview may be booked with. Deliberately longer than
    // RoomOpenLeadMinutes: booking inside that window would create an interview whose room is
    // already open at the moment it is created, and whose invitation email would land after the
    // candidate was expected to join. "In the future" alone is not a useful guarantee — an
    // interview five seconds out is nominally valid and practically nonsense.
    public const int MinimumLeadMinutes = 15;

    // The only durations a recruiter may pick, in minutes. A closed set instead of a free number so
    // the schedule stays sane (no 6000-minute interviews) and the UI can offer plain choices. The
    // web form mirrors this exact list.
    public static readonly IReadOnlyList<int> AllowedDurationMinutes = [10, 15, 20, 30, 45, 60];

    private readonly List<Guid> _interviewerUserIds = new();

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public InterviewType Type { get; private set; }
    public DateTime ScheduledAtUtc { get; private set; }
    public int DurationMinutes { get; private set; }

    // Unique locator for the (future) live room. Not a bearer secret hashed like an email-confirm
    // token: both the candidate and the interviewer revisit the same link repeatedly, and the
    // candidate portal needs to render it back to them, so it is stored as plain, unguessable
    // (256-bit random) text with a unique index rather than a one-time hashed token. The join
    // endpoint is what actually gates access (auth + participant membership + time window) — the
    // token only keeps the URL from being enumerable.
    //
    // Null for a phone screen: that interview happens over the phone, so there is no live room to
    // link to. A unique index tolerates many NULLs in PostgreSQL, so several phone screens coexist.
    public string? RoomToken { get; private set; }

    // The interviewers, stored as a native PostgreSQL uuid[] column rather than a child table: the
    // list is small, owned wholly by the interview, and queried with "is this user an interviewer"
    // (id = ANY(...)) which a GIN index serves. Exposed read-only; EF reads/writes the backing field.
    public IReadOnlyList<Guid> InterviewerUserIds => _interviewerUserIds;

    public InterviewStatus Status { get; private set; }
    public string? Notes { get; private set; }

    // Set together with Status.Cancelled, null in every other state. The reason is candidate-facing
    // (it picks the sentence in the cancellation email); the note is the recruiter's own wording and
    // stays on the company side, the same rule Notes already follows.
    public InterviewCancellationReason? CancellationReason { get; private set; }
    public string? CancellationNote { get; private set; }

    // Set together with Status.NoShow, null otherwise. See NoShowParty: "nobody came" is not a
    // usable record when it cannot say which side.
    public NoShowParty? NoShowParty { get; private set; }

    // When each reminder is owed, or null when none is — which covers both "already delivered" and
    // "never applicable", the two cases that need identical handling. Storing the due instant rather
    // than a sent-at timestamp is what lets a background sweep find work with a single indexed
    // "due <= now" predicate and clear the row by nulling it, so the index only ever holds pending
    // reminders. The cost is that the row does not remember that a reminder went out; the send is
    // logged instead, and an audit trail can be added the day something actually needs one.
    public DateTime? DayBeforeReminderDueAtUtc { get; private set; }
    public DateTime? StartingSoonReminderDueAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public Guid? DeletedBy { get; private set; }

    private Interview() { }

    private Interview(
        Guid id, Guid applicationId, InterviewType type, DateTime scheduledAtUtc,
        int durationMinutes, IEnumerable<Guid> interviewerUserIds, string? notes, DateTime nowUtc)
    {
        Id = id;
        ApplicationId = applicationId;
        Type = type;
        ScheduledAtUtc = scheduledAtUtc;
        DurationMinutes = durationMinutes;
        Notes = notes;
        Status = InterviewStatus.Scheduled;
        RoomToken = UsesRoom(type) ? GenerateRoomToken() : null;
        _interviewerUserIds.AddRange(interviewerUserIds);
        ScheduleReminders(nowUtc);
    }

    // nowUtc is passed in rather than read from the ambient clock, the same discipline IsRoomOpen and
    // CanReceiveFeedback already follow. Time is an input to these rules, so making it a parameter is
    // what lets "cancelling after the start time is rejected" be a unit test instead of a stopwatch.
    public static Interview Schedule(
        Guid applicationId, InterviewType type, DateTime scheduledAtUtc, int durationMinutes,
        IReadOnlyCollection<Guid> interviewerUserIds, DateTime nowUtc, string? notes = null)
    {
        if (applicationId == Guid.Empty)
            throw new ArgumentException("ApplicationId is required.", nameof(applicationId));
        if (interviewerUserIds is null || interviewerUserIds.Count == 0)
            throw new ArgumentException("At least one interviewer is required.", nameof(interviewerUserIds));
        if (interviewerUserIds.Any(id => id == Guid.Empty))
            throw new ArgumentException("Interviewer ids must not be empty.", nameof(interviewerUserIds));

        EnsureSlotIsBookable(scheduledAtUtc, durationMinutes, nowUtc, nameof(scheduledAtUtc));

        return new Interview(
            Guid.NewGuid(), applicationId, type, scheduledAtUtc, durationMinutes,
            interviewerUserIds.Distinct(), Normalize(notes), nowUtc);
    }

    // Moves the interview to a new time (and optionally a new duration). Only allowed before the
    // interview was due to start: once the slot has come and gone, moving it would erase the fact
    // that the original appointment was missed. Mark it NoShow and book a fresh one instead.
    public void Reschedule(DateTime newScheduledAtUtc, int newDurationMinutes, DateTime nowUtc)
    {
        EnsurePending("rescheduled", nowUtc);
        EnsureSlotIsBookable(newScheduledAtUtc, newDurationMinutes, nowUtc, nameof(newScheduledAtUtc));

        ScheduledAtUtc = newScheduledAtUtc;
        DurationMinutes = newDurationMinutes;

        // Recomputed from the new time, unconditionally. A reminder already sent for the old slot is
        // not credit against the new one — the candidate was told about a meeting that no longer
        // exists — and one still pending for the old slot would fire at a now-meaningless moment.
        ScheduleReminders(nowUtc);
    }

    // Cancelling means "this will not happen", so it is only truthful before the start time. After
    // that the interview either happened (Complete) or someone failed to appear (MarkNoShow) —
    // there is no third possibility, and offering one lets a recruiter file a false record.
    public void Cancel(InterviewCancellationReason reason, string? note, DateTime nowUtc)
    {
        if (!Enum.IsDefined(reason))
            throw new ArgumentException("Unknown cancellation reason.", nameof(reason));

        EnsurePending("cancelled", nowUtc);
        Status = InterviewStatus.Cancelled;
        CancellationReason = reason;
        CancellationNote = Normalize(note);
        ClearReminders();
    }

    // The mirror of Cancel: an interview cannot have been completed before it began. Requiring the
    // start time to have passed stops a recruiter from clearing tomorrow's calendar by marking it
    // all done today.
    public void Complete(DateTime nowUtc)
    {
        EnsureUnderway("completed", nowUtc);
        Status = InterviewStatus.Completed;
        ClearReminders();
    }

    public void MarkNoShow(NoShowParty party, DateTime nowUtc)
    {
        if (!Enum.IsDefined(party))
            throw new ArgumentException("Unknown no-show party.", nameof(party));

        EnsureUnderway("marked as a no-show", nowUtc);
        Status = InterviewStatus.NoShow;
        NoShowParty = party;
        ClearReminders();
    }

    // Swaps the panel without touching the time. Kept apart from Reschedule rather than folded into
    // it: "reschedule" that also silently changes who is in the room is a misleading name, and the
    // two have different reasons to be refused. Only allowed before the start — after that the
    // interview either happened with these people or did not happen at all, and rewriting the panel
    // would be falsifying the record rather than correcting a plan.
    public void ReassignInterviewers(IReadOnlyCollection<Guid> interviewerUserIds, DateTime nowUtc)
    {
        if (interviewerUserIds is null || interviewerUserIds.Count == 0)
            throw new ArgumentException("At least one interviewer is required.", nameof(interviewerUserIds));
        if (interviewerUserIds.Any(id => id == Guid.Empty))
            throw new ArgumentException("Interviewer ids must not be empty.", nameof(interviewerUserIds));

        EnsurePending("reassigned", nowUtc);

        _interviewerUserIds.Clear();
        _interviewerUserIds.AddRange(interviewerUserIds.Distinct());
    }

    // ---- Derived state ----
    // None of this is stored. The lifecycle is a pure function of (Status, ScheduledAtUtc,
    // DurationMinutes, now), so persisting it would duplicate facts the row already holds and open a
    // window where the column disagrees with the clock. Same reasoning as RoomOpensAtUtc below.

    public DateTime EndsAtUtc => ScheduledAtUtc.AddMinutes(DurationMinutes);

    public bool HasStarted(DateTime nowUtc) => nowUtc >= ScheduledAtUtc;

    public bool HasElapsed(DateTime nowUtc) => nowUtc >= EndsAtUtc;

    // The state the UI complained about: the slot is over but nobody recorded what happened. It is
    // still Status.Scheduled in the database — "scheduled" just stops being the whole truth once the
    // clock passes the end time, and this is what surfaces that difference.
    public bool IsAwaitingOutcome(DateTime nowUtc) =>
        Status == InterviewStatus.Scheduled && HasElapsed(nowUtc);

    // The four capability predicates the transitions guard on. Exposed so the API can hand the client
    // the same answers rather than have it re-derive them from timestamps — the duplicated rule is
    // exactly how the buttons drifted out of sync with the domain in the first place.
    public bool CanReschedule(DateTime nowUtc) => IsPending(nowUtc);
    public bool CanCancel(DateTime nowUtc) => IsPending(nowUtc);
    public bool CanReassignInterviewers(DateTime nowUtc) => IsPending(nowUtc);
    public bool CanComplete(DateTime nowUtc) => IsUnderway(nowUtc);
    public bool CanMarkNoShow(DateTime nowUtc) => IsUnderway(nowUtc);

    // Whether a reminder that has come due is still worth delivering. Deliberately the same
    // predicate the transitions guard on: a reminder is only useful while the meeting can still be
    // acted on, so "too late to remind" and "too late to move it" must not disagree. This is what
    // stops a sweep that ran late — after a restart, say — from telling a candidate to join an
    // interview that already started.
    public bool CanRemind(DateTime nowUtc) => IsPending(nowUtc);

    private bool IsPending(DateTime nowUtc) =>
        Status == InterviewStatus.Scheduled && !HasStarted(nowUtc);

    private bool IsUnderway(DateTime nowUtc) =>
        Status == InterviewStatus.Scheduled && HasStarted(nowUtc);

    // The room opens a fixed lead time before the scheduled start and stays reachable until a fixed
    // grace period after the scheduled end — computed on read, not stored, so there is nothing to
    // keep in sync when an interview is rescheduled or the query simply runs at a different time.
    public DateTime RoomOpensAtUtc => ScheduledAtUtc.AddMinutes(-RoomOpenLeadMinutes);

    public DateTime RoomClosesAtUtc => EndsAtUtc.AddMinutes(RoomCloseGraceMinutes);

    public bool IsRoomOpen(DateTime nowUtc) =>
        RoomToken is not null
        && Status == InterviewStatus.Scheduled
        && nowUtc >= RoomOpensAtUtc && nowUtc <= RoomClosesAtUtc;

    // ---- Reminders ----

    // Marks one reminder as no longer owed. Called by the sweep for every row it picks up, whether or
    // not it actually sent anything: a reminder that came due while the interview was cancelled is
    // just as settled as one that was delivered, and leaving it set would keep the row in the sweep's
    // index forever.
    public void ClearReminder(InterviewReminderKind kind)
    {
        switch (kind)
        {
            case InterviewReminderKind.DayBefore:
                DayBeforeReminderDueAtUtc = null;
                break;
            case InterviewReminderKind.StartingSoon:
                StartingSoonReminderDueAtUtc = null;
                break;
            default:
                throw new ArgumentException("Unknown reminder kind.", nameof(kind));
        }
    }

    private void ScheduleReminders(DateTime nowUtc)
    {
        DayBeforeReminderDueAtUtc =
            FutureOrNull(ScheduledAtUtc.AddHours(-DayBeforeReminderLeadHours), nowUtc);
        StartingSoonReminderDueAtUtc = FutureOrNull(RoomOpensAtUtc, nowUtc);
    }

    private void ClearReminders()
    {
        DayBeforeReminderDueAtUtc = null;
        StartingSoonReminderDueAtUtc = null;
    }

    // A reminder point that is already behind us is not owed. Booking (or moving) an interview to
    // tomorrow morning produces an invitation email carrying exactly the facts the day-before
    // reminder would carry, seconds earlier — scheduling it anyway would send the candidate the same
    // message twice in a row, which reads as a system fault rather than a courtesy.
    private static DateTime? FutureOrNull(DateTime dueAtUtc, DateTime nowUtc) =>
        dueAtUtc > nowUtc ? dueAtUtc : null;

    // Only an online interview type gets a live room; a phone screen happens over the phone. Keeping
    // this a single predicate means the room token, the candidate's "open room" link and the emailed
    // link all agree on exactly when a room exists.
    public static bool UsesRoom(InterviewType type) => type != InterviewType.PhoneScreen;

    // An interview can be evaluated only once it has actually taken place: either explicitly marked
    // Completed, or its scheduled end time has already passed. A still-future interview has nothing
    // to evaluate yet; a Cancelled or NoShow one never produced anything to evaluate.
    public bool CanReceiveFeedback(DateTime nowUtc) =>
        Status == InterviewStatus.Completed || IsAwaitingOutcome(nowUtc);

    // Shared by Schedule and Reschedule so a booking and a re-booking can never diverge on what
    // counts as a valid slot.
    private static void EnsureSlotIsBookable(
        DateTime scheduledAtUtc, int durationMinutes, DateTime nowUtc, string timeParameterName)
    {
        if (scheduledAtUtc < nowUtc.AddMinutes(MinimumLeadMinutes))
            throw new ArgumentException(
                $"An interview must be scheduled at least {MinimumLeadMinutes} minutes ahead.",
                timeParameterName);
        if (!AllowedDurationMinutes.Contains(durationMinutes))
            throw new ArgumentException(
                "Duration must be one of the allowed presets.", nameof(durationMinutes));
    }

    private void EnsurePending(string action, DateTime nowUtc)
    {
        EnsureScheduled(action);
        if (HasStarted(nowUtc))
            throw new InvalidOperationException(
                $"An interview that was due to start at {ScheduledAtUtc:u} cannot be {action}.");
    }

    private void EnsureUnderway(string action, DateTime nowUtc)
    {
        EnsureScheduled(action);
        if (!HasStarted(nowUtc))
            throw new InvalidOperationException(
                $"An interview that has not started yet cannot be {action}.");
    }

    private void EnsureScheduled(string action)
    {
        if (Status != InterviewStatus.Scheduled)
            throw new InvalidOperationException(
                $"An interview in status '{Status}' cannot be {action}.");
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Same construction as Tenants.InvitationService/CandidateProfileService's GenerateToken: 256
    // bits of CSPRNG randomness, URL-safe base64. Duplicated locally rather than shared — the
    // codebase's established preference for these few lines over a cross-module abstraction.
    private static string GenerateRoomToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}
