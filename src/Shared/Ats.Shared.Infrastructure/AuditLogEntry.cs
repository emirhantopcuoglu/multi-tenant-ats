namespace Ats.Shared.Infrastructure;

// One row per audited change, saved in the same transaction as the change. Not ITenantScoped: the
// interceptor sets TenantId itself and must not audit its own rows.
public sealed class AuditLogEntry
{
    public const int UserAgentMaxLength = 512;

    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid? ActorUserId { get; init; }
    public string? ActorEmail { get; init; }
    public string Action { get; init; } = "";
    public string ResourceType { get; init; } = "";
    public string ResourceId { get; init; } = "";
    // JSON of the properties: all of them on create and delete, only the changed ones on update.
    public string? Before { get; init; }
    public string? After { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}
