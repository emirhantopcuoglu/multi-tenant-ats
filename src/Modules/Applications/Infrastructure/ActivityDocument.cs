using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Ats.Modules.Applications.Infrastructure;

// Mongo storage model, separate from the domain ApplicationActivity. Guids are stored as strings.
internal sealed class ActivityDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    // Mongo has no query filter, so every document carries its tenant and every read filters on it.
    [BsonElement("tenantId")]
    [BsonRepresentation(BsonType.String)]
    public Guid TenantId { get; set; }

    [BsonElement("applicationId")]
    [BsonRepresentation(BsonType.String)]
    public Guid ApplicationId { get; set; }

    [BsonElement("activityType")]
    public string ActivityType { get; set; } = null!;

    // Null when the candidate acted, e.g. submitting or withdrawing.
    [BsonElement("actorUserId")]
    [BsonRepresentation(BsonType.String)]
    public Guid? ActorUserId { get; set; }

    // A nested document, not a string; the shape varies by activity type.
    [BsonElement("payload")]
    public BsonDocument Payload { get; set; } = new();

    [BsonElement("occurredAtUtc")]
    public DateTime OccurredAtUtc { get; set; }
}
