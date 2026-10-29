namespace Ats.Shared.Contracts.Applications;

// Self-contained: carries the tenant, ids, CV key and job id, so the parser needs no request
// context and can compare the CV with this job's requirements.
public sealed record CvParseRequestedIntegrationEvent(
    Guid ApplicationId,
    Guid JobId,
    Guid CandidateId,
    string CvFileKey,
    Guid TenantId);
