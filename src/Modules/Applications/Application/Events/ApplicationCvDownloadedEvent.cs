using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Raised in-process the first time a company user downloads an application's CV
// (MarkCvDownloadedHandler only fires it on the first download — see Application.MarkCvDownloaded).
// A handler in this module (PublishApplicationCvDownloadedIntegrationEvent) bridges it onto
// RabbitMQ for the in-app notification backbone. Same split as ApplicationViewedEvent.
public sealed record ApplicationCvDownloadedEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    Guid? CandidateAccountId,
    Guid TenantId) : INotification;
