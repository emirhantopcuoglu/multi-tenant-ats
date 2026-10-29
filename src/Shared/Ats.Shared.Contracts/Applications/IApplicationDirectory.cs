namespace Ats.Shared.Contracts.Applications;

// Read port into the Applications module. Runs in the current tenant, so another tenant's
// application looks missing.
public interface IApplicationDirectory
{
    Task<ApplicationForScheduling?> GetForSchedulingAsync(
        Guid applicationId, CancellationToken cancellationToken = default);

    // For callers with no ambient tenant; matches the given tenant explicitly.
    Task<ApplicationForScheduling?> GetForSchedulingAsync(
        Guid tenantId, Guid applicationId, CancellationToken cancellationToken = default);

    // Batched, for the reminder sweep across all tenants. No tenant parameter: the ids come from rows
    // the caller already owns. Unknown ids are left out.
    Task<IReadOnlyDictionary<Guid, ApplicationForScheduling>> GetForSchedulingAsync(
        IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken = default);

    // Candidate names by application id. Unknown ids are left out.
    Task<IReadOnlyDictionary<Guid, string>> GetCandidateNamesByApplicationAsync(
        IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken = default);

    // The candidate's applications in the current tenant, for the double-booking check.
    Task<IReadOnlyList<Guid>> GetApplicationIdsForCandidateAsync(
        Guid candidateId, CancellationToken cancellationToken = default);

    // The caller passes the window start.
    Task<int> CountApplicationsSinceAsync(
        DateTime sinceUtc, CancellationToken cancellationToken = default);

    // Distinct candidates with at least one active application.
    Task<int> CountActiveCandidatesAsync(CancellationToken cancellationToken = default);
}

// IsActive answers "can interviews still be scheduled?" so ApplicationStatus stays inside its module.
// Candidate contact and job title let Interviews publish self-contained events. JobTitle is empty if
// the job is gone; CandidateAccountId is null for applications made before candidate accounts.
public sealed record ApplicationForScheduling(
    Guid Id,
    bool IsActive,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    Guid? CandidateAccountId,
    string CandidateEmail,
    string CandidateFirstName);
