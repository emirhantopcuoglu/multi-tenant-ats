using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

public interface ICandidateEmailVerificationService
{
    // Mints a fresh link and mails it to the account's current address, superseding any pending one.
    // Called at registration and from the resend endpoint.
    //
    // Returns AlreadyVerified rather than silently succeeding: the caller here is always the signed-in
    // owner of the account, so unlike the password-reset request there is nothing to hide from them —
    // and a UI that says "sent" for a no-op would leave them waiting for an email that never comes.
    Task<Result> SendAsync(Guid candidateAccountId, CancellationToken cancellationToken = default);

    // Consumes a link. Idempotent from the candidate's point of view only in the sense that a second
    // click of the SAME link fails with InvalidToken — the address stays verified either way.
    Task<Result> ConfirmAsync(string token, CancellationToken cancellationToken = default);
}
