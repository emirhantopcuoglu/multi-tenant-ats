namespace Ats.Shared.Contracts.Interviews;

// Read port into the Interviews module. Runs in the current tenant.
public interface IInterviewDirectory
{
    // The caller passes "now" so it can be tested.
    Task<int> CountUpcomingInterviewsAsync(DateTime nowUtc, CancellationToken cancellationToken = default);

    // For the candidate's application detail, which spans tenants; the tenant is passed explicitly.
    Task<IReadOnlyList<CandidateInterviewInfo>> GetForApplicationAsync(
        Guid tenantId, Guid applicationId, CancellationToken cancellationToken = default);

    // Batched, for "My interviews". No tenant parameter: the caller already verified the ids belong
    // to the candidate.
    Task<IReadOnlyList<CandidateInterviewInfo>> GetForApplicationsAsync(
        IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken = default);
}

// Candidate-safe by shape: no interviewer ids or recruiter notes. RoomToken is null for phone screens.
public sealed record CandidateInterviewInfo(
    Guid Id,
    Guid ApplicationId,
    string Type,
    DateTime ScheduledAtUtc,
    int DurationMinutes,
    string Status,
    string? RoomToken);
