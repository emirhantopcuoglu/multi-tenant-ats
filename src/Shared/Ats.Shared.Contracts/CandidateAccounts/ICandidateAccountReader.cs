namespace Ats.Shared.Contracts.CandidateAccounts;

// IsEmailVerified travels on the summary rather than behind a second port method: the Applications
// module needs it on exactly the path that already loads this record (SubmitApplicationHandler), so a
// separate call would be a second round trip for a field the first one can carry.
//
// A bool, not the timestamp: the only question outside this module is "may they apply?". When the
// address was proven stays the CandidateAccounts module's business.
public sealed record CandidateAccountSummary(
    Guid Id, string Email, string FirstName, string LastName, bool IsEmailVerified);

public interface ICandidateAccountReader
{
    Task<CandidateAccountSummary?> GetByIdAsync(Guid id, CancellationToken ct = default);

    // Which language to write to this address in. Keyed on email rather than account id because the
    // candidate-facing integration events carry the address and not always the account (see
    // ApplicationSubmittedIntegrationEvent) — and the address is globally unique, so it identifies
    // the account just as precisely.
    //
    // Never null: a caller about to compose an email has no second move to make if the language is
    // unknown, so "no account for this address" resolves to the default here rather than being
    // re-decided at every send site. Accounts submitted before candidate logins existed, and
    // soft-deleted ones, both land on that default.
    //
    // This is one indexed lookup per email, on a path that is already asynchronous and out of the
    // request's way. It is deliberately a live read rather than a value frozen onto the event: a
    // candidate who switches language should have the next email follow, not the ones from before
    // the switch. That does mean the Notifications module can no longer build these emails from the
    // message alone — a real cost against the self-contained-message rule the contracts state, paid
    // knowingly because the alternative was a language field on nine contracts and a denormalized
    // copy in two more schemas.
    Task<string> GetPreferredLanguageByEmailAsync(string email, CancellationToken ct = default);
}
