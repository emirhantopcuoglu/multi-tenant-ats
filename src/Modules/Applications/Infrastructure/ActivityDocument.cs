using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Ats.Modules.Applications.Infrastructure;

// MongoDB persistence model for an activity log entry. Kept separate from the domain
// ApplicationActivity (which has private setters and factory-only construction) so the domain
// model stays pure and the storage shape can evolve independently — the same split as an EF
// entity vs. a read DTO, applied to a document store.
//
// Guid fields are stored as strings (BsonType.String) rather than the driver's binary UUID
// subtype: human-readable in Compass and free of the GuidRepresentation pitfalls in driver 3.x.
internal sealed class ActivityDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    // Tenant isolation is manual in Mongo (no global query filter), so every document carries
    // its tenant and every read filters on it. Stamped from ICurrentTenant in the repository.
    [BsonElement("tenantId")]
    [BsonRepresentation(BsonType.String)]
    public Guid TenantId { get; set; }

    [BsonElement("applicationId")]
    [BsonRepresentation(BsonType.String)]
    public Guid ApplicationId { get; set; }

    [BsonElement("activityType")]
    public string ActivityType { get; set; } = null!;

    // Null when an anonymous candidate triggered the activity (e.g. submitting an application).
    [BsonElement("actorUserId")]
    [BsonRepresentation(BsonType.String)]
    public Guid? ActorUserId { get; set; }

    // The schema-flexible part: a real nested document, not a string. Different activity types
    // store differently shaped payloads in the same field — exactly what a document store is for.
    [BsonElement("payload")]
    public BsonDocument Payload { get; set; } = new();

    [BsonElement("occurredAtUtc")]
    public DateTime OccurredAtUtc { get; set; }
}
