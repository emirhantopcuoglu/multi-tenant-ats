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
        int durationMinutes, IEnumerable<Guid> interviewerUserIds, string? notes)
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
    }

    public static Interview Schedule(
        Guid applicationId, InterviewType type, DateTime scheduledAtUtc, int durationMinutes,
        IReadOnlyCollection<Guid> interviewerUserIds, string? notes = null)
    {
        if (applicationId == Guid.Empty)
            throw new ArgumentException("ApplicationId is required.", nameof(applicationId));
        if (scheduledAtUtc <= DateTime.UtcNow)
            throw new ArgumentException("An interview must be scheduled in the future.", nameof(scheduledAtUtc));
        if (!AllowedDurationMinutes.Contains(durationMinutes))
            throw new ArgumentException("Duration must be one of the allowed presets.", nameof(durationMinutes));
        if (interviewerUserIds is null || interviewerUserIds.Count == 0)
            throw new ArgumentException("At least one interviewer is required.", nameof(interviewerUserIds));
        if (interviewerUserIds.Any(id => id == Guid.Empty))
            throw new ArgumentException("Interviewer ids must not be empty.", nameof(interviewerUserIds));

        return new Interview(
            Guid.NewGuid(), applicationId, type, scheduledAtUtc, durationMinutes,
            interviewerUserIds.Distinct(), Normalize(notes));
    }

    // Moves the interview to a new time (and optionally a new duration). Only a still-scheduled
    // interview can be rescheduled — a completed/cancelled one is settled.
    public void Reschedule(DateTime newScheduledAtUtc, int newDurationMinutes)
    {
        if (newScheduledAtUtc <= DateTime.UtcNow)
            throw new ArgumentException("An interview must be scheduled in the future.", nameof(newScheduledAtUtc));
        if (!AllowedDurationMinutes.Contains(newDurationMinutes))
            throw new ArgumentException("Duration must be one of the allowed presets.", nameof(newDurationMinutes));

        EnsureScheduled("rescheduled");
        ScheduledAtUtc = newScheduledAtUtc;
        DurationMinutes = newDurationMinutes;
    }

    public void Cancel()
    {
        EnsureScheduled("cancelled");
        Status = InterviewStatus.Cancelled;
    }

    public void Complete()
    {
        EnsureScheduled("completed");
        Status = InterviewStatus.Completed;
    }

    public void MarkNoShow()
    {
        EnsureScheduled("marked as a no-show");
        Status = InterviewStatus.NoShow;
    }

    // The room opens a fixed lead time before the scheduled start and stays reachable until a fixed
    // grace period after the scheduled end — computed on read, not stored, so there is nothing to
    // keep in sync when an interview is rescheduled or the query simply runs at a different time.
    public DateTime RoomOpensAtUtc => ScheduledAtUtc.AddMinutes(-RoomOpenLeadMinutes);

    public DateTime RoomClosesAtUtc =>
        ScheduledAtUtc.AddMinutes(DurationMinutes).AddMinutes(RoomCloseGraceMinutes);

    public bool IsRoomOpen(DateTime nowUtc) =>
        RoomToken is not null
        && Status == InterviewStatus.Scheduled
        && nowUtc >= RoomOpensAtUtc && nowUtc <= RoomClosesAtUtc;

    // Only an online interview type gets a live room; a phone screen happens over the phone. Keeping
    // this a single predicate means the room token, the candidate's "open room" link and the emailed
    // link all agree on exactly when a room exists.
    public static bool UsesRoom(InterviewType type) => type != InterviewType.PhoneScreen;

    // An interview can be evaluated only once it has actually taken place: either explicitly marked
    // Completed, or its scheduled end time has already passed. A still-future interview has nothing
    // to evaluate yet; a Cancelled or NoShow one never produced anything to evaluate.
    public bool CanReceiveFeedback(DateTime nowUtc) =>
        Status == InterviewStatus.Completed
        || (Status == InterviewStatus.Scheduled
            && nowUtc >= ScheduledAtUtc.AddMinutes(DurationMinutes));

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
