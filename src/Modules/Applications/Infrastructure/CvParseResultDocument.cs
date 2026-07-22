using Ats.Shared.Kernel;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Ats.Modules.Applications.Infrastructure;

// MongoDB persistence model for a CV parse result (Sprint 6.3). Stored in Mongo rather than Postgres
// for the same reasons as the activity log: schema-flexible, append-mostly, and produced out-of-band
// by a consumer. Kept separate from the Kernel's CvParseResult so the storage shape can evolve
// independently of the parse contract.
//
// The application id is the document _id: there is exactly one parse result per application, so an
// upsert keyed on it makes re-processing idempotent. tenantId is stored alongside so every read can
// confirm the caller's tenant owns the result. Guids are stored as strings (readable in Compass,
// free of the driver's UUID-representation pitfalls), matching ActivityDocument.
internal sealed class CvParseResultDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid ApplicationId { get; set; }

    [BsonElement("tenantId")]
    [BsonRepresentation(BsonType.String)]
    public Guid TenantId { get; set; }

    [BsonElement("skills")]
    public List<string> Skills { get; set; } = [];

    [BsonElement("totalExperienceYears")]
    public double TotalExperienceYears { get; set; }

    [BsonElement("education")]
    public List<EducationDocument> Education { get; set; } = [];

    [BsonElement("recentPositions")]
    public List<PositionDocument> RecentPositions { get; set; } = [];

    [BsonElement("jobFitRating")]
    [BsonRepresentation(BsonType.String)]
    public CvJobFitRating JobFitRating { get; set; }

    [BsonElement("fitSummary")]
    public string FitSummary { get; set; } = "";

    [BsonElement("matchedRequirements")]
    public List<string> MatchedRequirements { get; set; } = [];

    [BsonElement("missingRequirements")]
    public List<string> MissingRequirements { get; set; } = [];

    [BsonElement("parsedAtUtc")]
    public DateTime ParsedAtUtc { get; set; }
}

internal sealed class EducationDocument
{
    [BsonElement("degree")]
    public string Degree { get; set; } = "";

    [BsonElement("institution")]
    public string Institution { get; set; } = "";

    [BsonElement("year")]
    public int Year { get; set; }
}

internal sealed class PositionDocument
{
    [BsonElement("title")]
    public string Title { get; set; } = "";

    [BsonElement("company")]
    public string Company { get; set; } = "";

    [BsonElement("startDate")]
    public string StartDate { get; set; } = "";

    [BsonElement("endDate")]
    public string EndDate { get; set; } = "";
}
