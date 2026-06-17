using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// A domain event raised in-process via MediatR after an application is persisted. Nothing
// consumes it yet; Sprint 5 republishes it as an integration event onto RabbitMQ to drive the
// "application received" / "new application" emails. Publishing it now keeps the apply flow
// stable so that wiring is purely additive later.
//
// It carries ids and the candidate email only — no entity references — so a future
// out-of-process consumer can handle it without loading this module's aggregates.
public sealed record ApplicationSubmittedEvent(
    Guid ApplicationId,
    Guid JobId,
    Guid CandidateId,
    Guid TenantId,
    string CandidateEmail) : INotification;
