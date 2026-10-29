using System.Security.Cryptography;
using Ats.Shared.Kernel;

namespace Ats.Modules.Interviews.Domain;

// Aggregate root. References the application by id only.
public sealed class Interview : ITenantScoped, IAuditable, ISoftDeletable
{
    public const int RoomOpenLeadMinutes = 10;
    public const int RoomCloseGraceMinutes = 15;

    // The second reminder has no constant: it is due when the room opens.
    public const int DayBeforeReminderLeadHours = 24;

    // Longer than RoomOpenLeadMinutes, so a new interview's room is never already open.
    public const int MinimumLeadMinutes = 15;

    // The web form mirrors this list.
    public static readonly IReadOnlyList<int> AllowedDurationMinutes = [10, 15, 20, 30, 45, 60];

    private readonly List<Guid> _interviewerUserIds = new();

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public InterviewType Type { get; private set; }
    public DateTime ScheduledAtUtc { get; private set; }
    public int DurationMinutes { get; private set; }

    // Stored in plain text: both sides revisit the link, and the join endpoint does the real access
    // check. 256 random bits, so it can't be guessed. Null for phone screens.
    public string? RoomToken { get; private set; }

    // Stored as a uuid[] column with a GIN index for "is this user on the panel" queries.
    public IReadOnlyList<Guid> InterviewerUserIds => _interviewerUserIds;

    public InterviewStatus Status { get; private set; }
    public string? Notes { get; private set; }

    // Reason is candidate-facing; the note stays on the company side.
    public InterviewCancellationReason? CancellationReason { get; private set; }
    public string? CancellationNote { get; private set; }

    public NoShowParty? NoShowParty { get; private set; }

    // Null when no reminder is owed. Storing the due time lets the sweep use one indexed "due <= now"
    // query and clear it by nulling it.
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

    // nowUtc is a parameter so the time rules can be unit tested.
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

    // Only before the start time. After that, mark it no-show and book a new one.
    public void Reschedule(DateTime newScheduledAtUtc, int newDurationMinutes, DateTime nowUtc)
    {
        EnsurePending("rescheduled", nowUtc);
        EnsureSlotIsBookable(newScheduledAtUtc, newDurationMinutes, nowUtc, nameof(newScheduledAtUtc));

        ScheduledAtUtc = newScheduledAtUtc;
        DurationMinutes = newDurationMinutes;

        // Reminders are recomputed for the new time.
        ScheduleReminders(nowUtc);
    }

    // Only before the start time; after that it either happened or someone didn't show.
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

    // Only after the start time.
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

    // Only before the start time, so the record of who interviewed can't be rewritten.
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

    // Derived from Status, the schedule and the clock; nothing here is stored.

    public DateTime EndsAtUtc => ScheduledAtUtc.AddMinutes(DurationMinutes);

    public bool HasStarted(DateTime nowUtc) => nowUtc >= ScheduledAtUtc;

    public bool HasElapsed(DateTime nowUtc) => nowUtc >= EndsAtUtc;

    // Slot is over but no outcome was recorded. Still Scheduled in the database.
    public bool IsAwaitingOutcome(DateTime nowUtc) =>
        Status == InterviewStatus.Scheduled && HasElapsed(nowUtc);

    // Exposed so the API can pass the same answers to the client.
    public bool CanReschedule(DateTime nowUtc) => IsPending(nowUtc);
    public bool CanCancel(DateTime nowUtc) => IsPending(nowUtc);
    public bool CanReassignInterviewers(DateTime nowUtc) => IsPending(nowUtc);
    public bool CanComplete(DateTime nowUtc) => IsUnderway(nowUtc);
    public bool CanMarkNoShow(DateTime nowUtc) => IsUnderway(nowUtc);

    // Same rule as the transitions, so a late sweep doesn't remind about an interview that started.
    public bool CanRemind(DateTime nowUtc) => IsPending(nowUtc);

    private bool IsPending(DateTime nowUtc) =>
        Status == InterviewStatus.Scheduled && !HasStarted(nowUtc);

    private bool IsUnderway(DateTime nowUtc) =>
        Status == InterviewStatus.Scheduled && HasStarted(nowUtc);

    // Computed on read, so nothing needs updating on reschedule.
    public DateTime RoomOpensAtUtc => ScheduledAtUtc.AddMinutes(-RoomOpenLeadMinutes);

    public DateTime RoomClosesAtUtc => EndsAtUtc.AddMinutes(RoomCloseGraceMinutes);

    public bool IsRoomOpen(DateTime nowUtc) =>
        RoomToken is not null
        && Status == InterviewStatus.Scheduled
        && nowUtc >= RoomOpensAtUtc && nowUtc <= RoomClosesAtUtc;


    // Called for every reminder the sweep picks up, sent or not, so the row leaves the index.
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

    // A reminder time already in the past isn't owed; the invitation covers it.
    private static DateTime? FutureOrNull(DateTime dueAtUtc, DateTime nowUtc) =>
        dueAtUtc > nowUtc ? dueAtUtc : null;

    public static bool UsesRoom(InterviewType type) => type != InterviewType.PhoneScreen;

    // Completed, or the scheduled end has passed.
    public bool CanReceiveFeedback(DateTime nowUtc) =>
        Status == InterviewStatus.Completed || IsAwaitingOutcome(nowUtc);

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

    private static string GenerateRoomToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}
