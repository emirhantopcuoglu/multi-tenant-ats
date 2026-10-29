using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Carries the CV key, job and tenant, so the consumer needs no request context.
public sealed record CvParseRequestedEvent(
    Guid ApplicationId,
    Guid JobId,
    Guid CandidateId,
    string CvFileKey,
    Guid TenantId) : INotification;
