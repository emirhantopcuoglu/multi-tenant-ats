using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Raised in-process via MediatR after an application is persisted, alongside ApplicationSubmittedEvent.
// A handler in this module (PublishCvParseRequestedIntegrationEvent) bridges it onto RabbitMQ, where
// the CV-parsing consumer picks it up to extract structured data from the candidate's CV.
//
// It carries the CV's object key, the job being applied to, and the owning tenant so the
// out-of-process consumer can download and parse the file, compare it against the job's
// requirements, and stamp the result with the tenant, without loading this module's aggregates
// or relying on an HTTP request's tenant context.
public sealed record CvParseRequestedEvent(
    Guid ApplicationId,
    Guid JobId,
    Guid CandidateId,
    string CvFileKey,
    Guid TenantId) : INotification;
