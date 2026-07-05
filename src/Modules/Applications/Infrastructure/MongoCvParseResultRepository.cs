using Ats.Modules.Applications.Application;
using Ats.Shared.Kernel;
using MongoDB.Driver;

namespace Ats.Modules.Applications.Infrastructure;

// MongoDB-backed CV parse result store (Sprint 6.3). Like MongoActivityLogRepository it uses the
// native driver and handles tenant isolation by hand: the write stamps the tenant from the message,
// the read filters on the current tenant.
//
// We deliberately add no custom index: the only read is by application id, which is the document _id
// (already uniquely indexed), and the tenant equality is a cheap check on that single document. An
// extra index would only slow writes with no query to serve (the same YAGNI reasoning as the
// activity log's single-index choice).
public sealed class MongoCvParseResultRepository : ICvParseResultRepository
{
    internal const string CollectionName = "cv_parse_results";

    private readonly IMongoCollection<CvParseResultDocument> _collection;
    private readonly ICurrentTenant _currentTenant;

    public MongoCvParseResultRepository(IMongoDatabase database, ICurrentTenant currentTenant)
    {
        _collection = database.GetCollection<CvParseResultDocument>(CollectionName);
        _currentTenant = currentTenant;
    }

    public async Task SaveAsync(
        Guid tenantId, Guid applicationId, CvParseResult result, DateTime parsedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var document = new CvParseResultDocument
        {
            ApplicationId = applicationId,
            TenantId = tenantId,
            Skills = result.Skills.ToList(),
            TotalExperienceYears = result.TotalExperienceYears,
            Education = result.Education
                .Select(e => new EducationDocument { Degree = e.Degree, Institution = e.Institution, Year = e.Year })
                .ToList(),
            RecentPositions = result.RecentPositions
                .Select(p => new PositionDocument
                {
                    Title = p.Title,
                    Company = p.Company,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate
                })
                .ToList(),
            JobFitRating = result.JobFitRating,
            FitSummary = result.FitSummary,
            MatchedRequirements = result.MatchedRequirements.ToList(),
            MissingRequirements = result.MissingRequirements.ToList(),
            ParsedAtUtc = parsedAtUtc
        };

        // Upsert keyed on the application id (the _id) so a redelivered message overwrites the prior
        // result rather than creating a duplicate — the parse is naturally idempotent.
        var filter = Builders<CvParseResultDocument>.Filter.Eq(d => d.ApplicationId, applicationId);
        await _collection.ReplaceOneAsync(
            filter, document, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<StoredCvParseResult?> GetByApplicationAsync(
        Guid applicationId, CancellationToken cancellationToken = default)
    {
        // No tenant means no safe scope to read in — return nothing rather than leak across tenants.
        if (_currentTenant.TenantId is not { } tenantId)
            return null;

        var filter = Builders<CvParseResultDocument>.Filter.Eq(d => d.TenantId, tenantId)
            & Builders<CvParseResultDocument>.Filter.Eq(d => d.ApplicationId, applicationId);

        var document = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        if (document is null)
            return null;

        return new StoredCvParseResult(
            document.ApplicationId,
            new CvParseResult(
                document.Skills,
                document.TotalExperienceYears,
                document.Education.Select(e => new CvEducation(e.Degree, e.Institution, e.Year)).ToList(),
                document.RecentPositions
                    .Select(p => new CvPosition(p.Title, p.Company, p.StartDate, p.EndDate))
                    .ToList(),
                document.JobFitRating,
                document.FitSummary,
                document.MatchedRequirements,
                document.MissingRequirements),
            document.ParsedAtUtc);
    }
}
