namespace Ats.Modules.Notifications.Domain;

public enum NotificationRecipientType
{
    // A global candidate account; its feed spans all tenants.
    Candidate = 1,

    // A company user. These rows also carry their TenantId.
    CompanyUser = 2,
}

// Tells the client which payload shape and template to use.
public enum NotificationType
{
    ApplicationStageChanged = 1,
    InterviewScheduled = 2,
    ApplicationViewed = 3,
    ApplicationCvDownloaded = 4,
    NewApplication = 5,
    InterviewRescheduled = 6,
    InterviewCancelled = 7,

    // One value for both reminders; the payload's reminderKind picks the sentence.
    InterviewReminder = 8,
}

// Payload is structured data, never pre-rendered text: the client renders it in its own language.
public sealed class Notification
{
    public Guid Id { get; private set; }
    public NotificationRecipientType RecipientType { get; private set; }
    public Guid RecipientId { get; private set; }

    // Null for candidates. Plain data, not a filter: this context has no tenant filter.
    public Guid? TenantId { get; private set; }

    public NotificationType Type { get; private set; }
    public string Payload { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    // Null means unread.
    public DateTime? ReadAtUtc { get; private set; }

    private Notification() { }

    private Notification(
        Guid id,
        NotificationRecipientType recipientType,
        Guid recipientId,
        Guid? tenantId,
        NotificationType type,
        string payload,
        DateTime createdAtUtc)
    {
        Id = id;
        RecipientType = recipientType;
        RecipientId = recipientId;
        TenantId = tenantId;
        Type = type;
        Payload = payload;
        CreatedAtUtc = createdAtUtc;
    }

    public static Notification ForCandidate(Guid candidateAccountId, NotificationType type, string payload)
    {
        if (candidateAccountId == Guid.Empty)
            throw new ArgumentException("A candidate account id is required.", nameof(candidateAccountId));
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("A payload is required.", nameof(payload));

        return new Notification(
            Guid.NewGuid(), NotificationRecipientType.Candidate, candidateAccountId,
            tenantId: null, type, payload, DateTime.UtcNow);
    }

    public static Notification ForCompanyUser(
        Guid tenantId, Guid userId, NotificationType type, string payload)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant id is required.", nameof(tenantId));
        if (userId == Guid.Empty)
            throw new ArgumentException("A user id is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("A payload is required.", nameof(payload));

        return new Notification(
            Guid.NewGuid(), NotificationRecipientType.CompanyUser, userId,
            tenantId, type, payload, DateTime.UtcNow);
    }

    // A second call keeps the original timestamp.
    public void MarkRead()
    {
        ReadAtUtc ??= DateTime.UtcNow;
    }
}
