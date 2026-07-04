using Ats.Shared.Kernel;

namespace Ats.Modules.Applications.Domain;

// One candidate's application to one job. Aggregate root. It references the job, candidate
// and current stage by id only — it never holds those objects — so the three aggregates stay
// independent. The lifecycle (Active -> terminal) is enforced here, not in a service.
public sealed class Application : ITenantScoped, IAuditable, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid JobId { get; private set; }
    public Guid CandidateId { get; private set; }
    public Guid? CandidateAccountId { get; private set; }
    public Guid CurrentStageId { get; private set; }
    public string CvFileKey { get; private set; } = null!;
    public string? CoverLetter { get; private set; }
    public ApplicationStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime AppliedAtUtc { get; private set; }
    // Read receipt for candidate transparency: when someone at the company first opened this
    // application. Later views never move it — "first viewed" is the honest signal.
    public DateTime? FirstViewedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public Guid? DeletedBy { get; private set; }

    private Application() { }

    private Application(
        Guid id, Guid jobId, Guid candidateId, Guid? candidateAccountId,
        Guid initialStageId, string cvFileKey, string? coverLetter)
    {
        Id = id;
        JobId = jobId;
        CandidateId = candidateId;
        CandidateAccountId = candidateAccountId;
        CurrentStageId = initialStageId;
        CvFileKey = cvFileKey;
        CoverLetter = coverLetter;
        Status = ApplicationStatus.Active;
        AppliedAtUtc = DateTime.UtcNow;
    }

    public static Application Create(
        Guid jobId, Guid candidateId, Guid? candidateAccountId,
        Guid initialStageId, string cvFileKey, string? coverLetter = null)
    {
        if (jobId == Guid.Empty)
            throw new ArgumentException("JobId is required.", nameof(jobId));
        if (candidateId == Guid.Empty)
            throw new ArgumentException("CandidateId is required.", nameof(candidateId));
        if (initialStageId == Guid.Empty)
            throw new ArgumentException("Initial stage is required.", nameof(initialStageId));
        if (string.IsNullOrWhiteSpace(cvFileKey))
            throw new ArgumentException("A CV file is required.", nameof(cvFileKey));

        return new Application(
            Guid.NewGuid(), jobId, candidateId, candidateAccountId, initialStageId, cvFileKey,
            string.IsNullOrWhiteSpace(coverLetter) ? null : coverLetter);
    }

    // Recruiter advances the application. Moving to a stage that does not belong to this
    // job's pipeline is a cross-aggregate check the command handler does (it has the
    // pipeline); the entity only guards that the application is still in play.
    public void MoveToStage(Guid stageId)
    {
        if (stageId == Guid.Empty)
            throw new ArgumentException("Stage is required.", nameof(stageId));

        EnsureActive("moved to another stage");
        CurrentStageId = stageId;
    }

    public void Reject(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A rejection reason is required.", nameof(reason));

        EnsureActive("rejected");
        Status = ApplicationStatus.Rejected;
        RejectionReason = reason.Trim();
    }

    public void Hire()
    {
        EnsureActive("hired");
        Status = ApplicationStatus.Hired;
    }

    public void Withdraw()
    {
        EnsureActive("withdrawn");
        Status = ApplicationStatus.Withdrawn;
    }

    // Returns true only on the first call so the caller knows whether to record a timeline
    // entry. Deliberately valid in any status: a terminal application can still be opened, and
    // the candidate deserves to know it was looked at.
    public bool MarkViewed()
    {
        if (FirstViewedAtUtc is not null)
            return false;

        FirstViewedAtUtc = DateTime.UtcNow;
        return true;
    }

    private void EnsureActive(string action)
    {
        if (Status != ApplicationStatus.Active)
            throw new InvalidOperationException(
                $"An application in status '{Status}' cannot be {action}.");
    }
}
