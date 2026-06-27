using Ats.Shared.Kernel;

namespace Ats.Modules.Applications.Application;

// Port over the persistence of CV parse results, which (like the activity log) live in MongoDB.
// The abstraction never mentions Mongo — handlers and the consumer depend on behaviour.
//
// The write side takes the tenant explicitly because its only caller is the CV-parsing consumer,
// which runs outside an HTTP request and therefore has no resolved ICurrentTenant — the tenant
// travels in the integration event instead. The read side has no such parameter: it runs inside a
// recruiter request, so the implementation scopes the read to the current tenant itself (the same
// asymmetry the outbox delivery service has with tenant context).
public interface ICvParseResultRepository
{
    Task SaveAsync(
        Guid tenantId, Guid applicationId, CvParseResult result, DateTime parsedAtUtc,
        CancellationToken cancellationToken = default);

    Task<StoredCvParseResult?> GetByApplicationAsync(
        Guid applicationId, CancellationToken cancellationToken = default);
}

// Read model for a stored parse result: the parsed data plus when it was produced.
public sealed record StoredCvParseResult(
    Guid ApplicationId, CvParseResult Result, DateTime ParsedAtUtc);
