using Ats.Shared.Kernel;

namespace Ats.Modules.Interviews.Domain;

// A single interview scheduled against one application. Aggregate root. It references the
// application by id only — never the Application object — so the two aggregates stay independent
// (the same discipline as Application referencing its job/candidate by id). The lifecycle
// (Scheduled -> terminal) is enforced here, not in a service.
public sealed class Interview : ITenantScoped, IAuditable, ISoftDeletable
{
    private readonly List<Guid> _interviewerUserIds = new();

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public InterviewType Type { get; private set; }
    public DateTime ScheduledAtUtc { get; private set; }
    public int DurationMinutes { get; private set; }
    public string? Location { get; private set; }

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
        int durationMinutes, string? location, IEnumerable<Guid> interviewerUserIds, string? notes)
    {
        Id = id;
        ApplicationId = applicationId;
        Type = type;
        ScheduledAtUtc = scheduledAtUtc;
        DurationMinutes = durationMinutes;
        Location = location;
        Notes = notes;
        Status = InterviewStatus.Scheduled;
        _interviewerUserIds.AddRange(interviewerUserIds);
    }

    public static Interview Schedule(
        Guid applicationId, InterviewType type, DateTime scheduledAtUtc, int durationMinutes,
        string? location, IReadOnlyCollection<Guid> interviewerUserIds, string? notes = null)
    {
        if (applicationId == Guid.Empty)
            throw new ArgumentException("ApplicationId is required.", nameof(applicationId));
        if (scheduledAtUtc <= DateTime.UtcNow)
            throw new ArgumentException("An interview must be scheduled in the future.", nameof(scheduledAtUtc));
        if (durationMinutes <= 0)
            throw new ArgumentException("Duration must be positive.", nameof(durationMinutes));
        if (interviewerUserIds is null || interviewerUserIds.Count == 0)
            throw new ArgumentException("At least one interviewer is required.", nameof(interviewerUserIds));
        if (interviewerUserIds.Any(id => id == Guid.Empty))
            throw new ArgumentException("Interviewer ids must not be empty.", nameof(interviewerUserIds));

        return new Interview(
            Guid.NewGuid(), applicationId, type, scheduledAtUtc, durationMinutes,
            Normalize(location), interviewerUserIds.Distinct(), Normalize(notes));
    }

    // Moves the interview to a new time (and optionally a new duration). Only a still-scheduled
    // interview can be rescheduled — a completed/cancelled one is settled.
    public void Reschedule(DateTime newScheduledAtUtc, int newDurationMinutes)
    {
        if (newScheduledAtUtc <= DateTime.UtcNow)
            throw new ArgumentException("An interview must be scheduled in the future.", nameof(newScheduledAtUtc));
        if (newDurationMinutes <= 0)
            throw new ArgumentException("Duration must be positive.", nameof(newDurationMinutes));

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

    private void EnsureScheduled(string action)
    {
        if (Status != InterviewStatus.Scheduled)
            throw new InvalidOperationException(
                $"An interview in status '{Status}' cannot be {action}.");
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
