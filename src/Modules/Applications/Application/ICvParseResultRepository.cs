using Ats.Shared.Kernel;

namespace Ats.Modules.Applications.Application;

// Stored in MongoDB. Writes take the tenant explicitly (the consumer has none); reads use the
// current tenant.
public interface ICvParseResultRepository
{
    Task SaveAsync(
        Guid tenantId, Guid applicationId, CvParseResult result, DateTime parsedAtUtc,
        CancellationToken cancellationToken = default);

    Task<StoredCvParseResult?> GetByApplicationAsync(
        Guid applicationId, CancellationToken cancellationToken = default);
}

public sealed record StoredCvParseResult(
    Guid ApplicationId, CvParseResult Result, DateTime ParsedAtUtc);
