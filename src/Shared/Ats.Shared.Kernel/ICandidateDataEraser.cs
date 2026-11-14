namespace Ats.Shared.Kernel;

// Implemented by each module that keeps a candidate's personal data outside their account, so the
// account erasure can reach it without knowing the modules. Must be safe to run twice: a failed
// erasure starts over the next night.
public interface ICandidateDataEraser
{
    Task EraseAsync(Guid candidateAccountId, CancellationToken cancellationToken = default);
}
