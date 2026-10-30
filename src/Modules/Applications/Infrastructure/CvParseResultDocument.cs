using Ats.Shared.Kernel;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Ats.Modules.Applications.Infrastructure;

// Mongo storage model. The application id is the _id, so re-processing upserts the same document.
// Guids are stored as strings.
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
