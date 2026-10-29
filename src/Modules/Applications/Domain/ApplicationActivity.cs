using System.Text.Json;
using Ats.Shared.Kernel;

namespace Ats.Modules.Applications.Domain;

// Append-only history entry with a JSON payload, stored in MongoDB. Never edited or deleted, so
// not IAuditable or ISoftDeletable.
public sealed class ApplicationActivity : ITenantScoped
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public ApplicationActivityType ActivityType { get; private set; }
    // Null when the candidate acted, e.g. submitting or withdrawing.
    public Guid? ActorUserId { get; private set; }
    // JSON; the shape depends on ActivityType.
    public string Payload { get; private set; } = "{}";
    public DateTime OccurredAtUtc { get; private set; }

    private ApplicationActivity() { }

    private ApplicationActivity(
        Guid applicationId, ApplicationActivityType type, Guid? actorUserId, string payload)
    {
        Id = Guid.NewGuid();
        ApplicationId = applicationId;
        ActivityType = type;
        ActorUserId = actorUserId;
        Payload = payload;
        OccurredAtUtc = DateTime.UtcNow;
    }

    public static ApplicationActivity Submitted(Guid applicationId, Guid jobId, string candidateEmail) =>
        new(applicationId, ApplicationActivityType.Submitted, actorUserId: null,
            JsonSerializer.Serialize(new { jobId, candidateEmail }));

    public static ApplicationActivity StageChanged(
        Guid applicationId, Guid? actorUserId, Guid fromStageId, Guid toStageId) =>
        new(applicationId, ApplicationActivityType.StageChanged, actorUserId,
            JsonSerializer.Serialize(new { fromStageId, toStageId }));

    // Always has a reason.
    public static ApplicationActivity StageCorrected(
        Guid applicationId, Guid? actorUserId, Guid fromStageId, Guid toStageId, string reason) =>
        new(applicationId, ApplicationActivityType.StageCorrected, actorUserId,
            JsonSerializer.Serialize(new { fromStageId, toStageId, reason }));

    public static ApplicationActivity Rejected(Guid applicationId, Guid? actorUserId, string reason) =>
        new(applicationId, ApplicationActivityType.Rejected, actorUserId,
            JsonSerializer.Serialize(new { reason }));

    public static ApplicationActivity Hired(Guid applicationId, Guid? actorUserId) =>
        new(applicationId, ApplicationActivityType.Hired, actorUserId, "{}");

    // The actor is kept for internal audit and never shown to the candidate.
    public static ApplicationActivity Viewed(Guid applicationId, Guid? actorUserId) =>
        new(applicationId, ApplicationActivityType.Viewed, actorUserId, "{}");

    // No actor: only the candidate can withdraw, and ActorUserId is for company users.
    public static ApplicationActivity Withdrawn(Guid applicationId) =>
        new(applicationId, ApplicationActivityType.Withdrawn, actorUserId: null, "{}");
}
