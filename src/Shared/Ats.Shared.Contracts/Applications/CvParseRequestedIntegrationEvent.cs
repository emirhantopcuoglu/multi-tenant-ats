namespace Ats.Shared.Contracts.Applications;

// Published onto the message bus when an application is submitted, and consumed by the CV-parsing
// worker to extract structured data from the candidate's CV. Like ApplicationSubmittedIntegrationEvent
// it lives in the neutral Contracts assembly so the publisher (Applications) and the consumer never
// reference each other.
//
// The message is self-contained — it carries the tenant, the application/candidate ids, and the
// object key of the already-uploaded CV — so the consumer downloads and parses the file without
// loading any of the Applications module's aggregates, and without an HTTP request's tenant context.
// TenantId travels in the message because the consumer runs outside a resolved-tenant request and
// must stamp the parse result with the owning tenant itself.
public sealed record CvParseRequestedIntegrationEvent(
    Guid ApplicationId,
    Guid CandidateId,
    string CvFileKey,
    Guid TenantId);
