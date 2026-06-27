using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Ats.Shared.Infrastructure;

// BsonRepresentation(String) on Guid fields avoids the driver-v3 GuidRepresentation pitfall
// ("GuidSerializer cannot serialize a Guid when GuidRepresentation is Unspecified"). Storing
// Guids as strings makes them human-readable in Compass, matches ActivityDocument and
// CvParseResultDocument, and costs nothing at the scale of an audit log.
internal sealed class AuditLogDocument
{
    [BsonId]
    public ObjectId Id { get; init; } = ObjectId.GenerateNewId();

    [BsonRepresentation(BsonType.String)]
    public Guid TenantId { get; init; }

    [BsonRepresentation(BsonType.String)]
    public Guid? ActorUserId { get; init; }

    public string? ActorEmail { get; init; }
    public string Action { get; init; } = default!;        // Created | Updated | Deleted
    public string ResourceType { get; init; } = default!;  // entity class name
    public string ResourceId { get; init; } = default!;    // entity primary key as string
    [BsonIgnoreIfNull]
    public BsonDocument? Before { get; init; }

    [BsonIgnoreIfNull]
    public BsonDocument? After { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}
