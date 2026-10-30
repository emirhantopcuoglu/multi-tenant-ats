using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

public interface ICandidateEmailVerificationService
{
    // Returns AlreadyVerified instead of a silent success, so the UI doesn't say "sent" for nothing.
    Task<Result> SendAsync(Guid candidateAccountId, CancellationToken cancellationToken = default);

    // A second click on the same link returns InvalidToken; the address stays verified.
    Task<Result> ConfirmAsync(string token, CancellationToken cancellationToken = default);
}
