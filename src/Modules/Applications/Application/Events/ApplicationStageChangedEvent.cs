using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Raised in-process after a recruiter moves an application to a new stage. A handler in this
// module (PublishApplicationStageChangedIntegrationEvent) bridges it onto RabbitMQ — the same
// split as ApplicationSubmittedEvent/ApplicationRejectedEvent: the domain event stays inside the
// module, the integration event crosses it.
//
// It carries plain data — candidate contact, job title and the resolved stage names — because the
// command handler is the only place that has the pipeline loaded; a consumer must be able to
// describe the transition without reloading any aggregate. CandidateAccountId is the global
// marketplace account (nullable: pre-account applications have none) — the address an in-app
// notification is routed to, as opposed to the per-tenant CandidateId.
public sealed record ApplicationStageChangedEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    Guid? CandidateAccountId,
    string CandidateEmail,
    string CandidateFirstName,
    Guid FromStageId,
    string FromStageName,
    Guid ToStageId,
    string ToStageName,
    Guid TenantId) : INotification;
