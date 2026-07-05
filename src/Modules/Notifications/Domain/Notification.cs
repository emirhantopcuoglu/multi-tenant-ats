namespace Ats.Modules.Notifications.Domain;

// Who a notification is addressed to. The recipient model covers both sides of the marketplace up
// front — the schema is the expensive thing to change later, the enum value is free today.
public enum NotificationRecipientType
{
    // A global marketplace candidate account (the CandidateAccounts module's identity). Not
    // tenant-scoped: the same account applies across every tenant's jobs, so its notifications
    // form one cross-tenant feed.
    Candidate = 1,

    // A company user inside a tenant. Rows for these recipients carry the TenantId they belong
    // to. Nothing creates them yet — they arrive with the company-side "new application"
    // notification step of the backbone.
    CompanyUser = 2,
}

// What happened. One value per event the backbone consumes; the type tells the frontend which
// payload shape to expect and which localized template to render.
public enum NotificationType
{
    ApplicationStageChanged = 1,
    InterviewScheduled = 2,
}

// A single in-app notification: an addressed, timestamped fact with a read marker. The payload is
// a JSON document of structured facts (job title, stage names, interview time — see
// NotificationPayloads in Infrastructure), never pre-rendered text: rendering, and therefore
// language, belongs to the client. The entity itself has no navigation anywhere — notifications
// reference nothing and nothing references them, so writes and deletes stay trivially cheap.
public sealed class Notification
{
    public Guid Id { get; private set; }
    public NotificationRecipientType RecipientType { get; private set; }
    public Guid RecipientId { get; private set; }

    // The tenant a CompanyUser recipient belongs to; null for Candidate recipients, whose feed is
    // global by design. Data, not a scope: this context has no tenant query filter.
    public Guid? TenantId { get; private set; }

    public NotificationType Type { get; private set; }
    public string Payload { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    // Null means unread. The timestamp (rather than a bool) preserves when the recipient saw it,
    // which the read-state queries get for free.
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

    // Idempotent: a second mark keeps the original timestamp, so a double-click or a replayed
    // request never rewrites when the notification was actually seen.
    public void MarkRead()
    {
        ReadAtUtc ??= DateTime.UtcNow;
    }
}
