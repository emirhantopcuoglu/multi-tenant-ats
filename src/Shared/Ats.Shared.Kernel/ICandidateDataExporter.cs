namespace Ats.Shared.Kernel;

// Implemented by each module that keeps data about a candidate, so the account can hand the candidate a
// copy of all of it (KVKK right of access) without knowing the modules.
public interface ICandidateDataExporter
{
    // The section's name in the export.
    string Section { get; }

    Task<object> ExportAsync(Guid candidateAccountId, CancellationToken cancellationToken = default);
}
