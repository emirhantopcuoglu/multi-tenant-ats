using System.Text.Json;
using Ats.Shared.Kernel;

namespace Ats.Modules.Applications.Domain;

// An append-only entry in an application's history: who did what, when, with a small JSON
// payload describing the change. Stored in PostgreSQL (jsonb) for now; it moves to MongoDB
// later, which is why the payload is a flexible document rather than typed columns.
//
// Unlike the other aggregates it is ITenantScoped but not IAuditable/ISoftDeletable: a log is
// never edited or deleted, and OccurredAtUtc plus ActorUserId already capture the "when/who".
public sealed class ApplicationActivity : ITenantScoped
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public ApplicationActivityType ActivityType { get; private set; }
    // Null for actions taken by an anonymous candidate (e.g. submitting an application).
    public Guid? ActorUserId { get; private set; }
    // Serialized JSON, mapped to a jsonb column. Shape depends on ActivityType.
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

    public static ApplicationActivity Rejected(Guid applicationId, Guid? actorUserId, string reason) =>
        new(applicationId, ApplicationActivityType.Rejected, actorUserId,
            JsonSerializer.Serialize(new { reason }));
}
