namespace Ats.Shared.Contracts.Applications;

// Published when a candidate withdraws their own application. Lives under Applications/ rather than
// Notifications/ because nothing is emailed for it: the candidate performed the action and already
// knows, so the only consumer is the Interviews module, which releases the slots that were booked
// for a process the candidate has left.
//
// Deliberately carries only what that consumer needs. The rejected/hired events carry the candidate
// name, email and job title because their consumers build an email from them; there is no such
// consumer here, and speculative fields on a message contract are the expensive kind of unused code
// — every future consumer inherits them.
public sealed record ApplicationWithdrawnIntegrationEvent(
    Guid ApplicationId,
    Guid TenantId);
