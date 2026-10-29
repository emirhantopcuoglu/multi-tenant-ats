using Ats.Shared.Kernel;

namespace Ats.Modules.Applications.Domain;

// Aggregate root. References the job, candidate and stage by id only.
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
    // When someone at the company first opened it. Later views don't change it.
    public DateTime? FirstViewedAtUtc { get; private set; }

    // When someone at the company first downloaded the CV.
    public DateTime? FirstCvDownloadedAtUtc { get; private set; }

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

    // The handler checks the stage belongs to this pipeline; the entity only checks the status.
    public void MoveToStage(Guid stageId)
    {
        if (stageId == Guid.Empty)
            throw new ArgumentException("Stage is required.", nameof(stageId));

        EnsureActive("moved to another stage");
        CurrentStageId = stageId;
    }

    // Takes the terminal stage so status and stage always change together.
    public void Reject(string reason, Guid rejectedStageId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A rejection reason is required.", nameof(reason));
        if (rejectedStageId == Guid.Empty)
            throw new ArgumentException("The rejected stage is required.", nameof(rejectedStageId));

        EnsureActive("rejected");
        Status = ApplicationStatus.Rejected;
        RejectionReason = reason.Trim();
        CurrentStageId = rejectedStageId;
    }

    public void Hire(Guid hiredStageId)
    {
        if (hiredStageId == Guid.Empty)
            throw new ArgumentException("The hired stage is required.", nameof(hiredStageId));

        EnsureActive("hired");
        Status = ApplicationStatus.Hired;
        CurrentStageId = hiredStageId;
    }

    public void Withdraw()
    {
        EnsureActive("withdrawn");
        Status = ApplicationStatus.Withdrawn;
    }

    // True only on the first call. Allowed in any status: a closed application can still be opened.
    public bool MarkViewed()
    {
        if (FirstViewedAtUtc is not null)
            return false;

        FirstViewedAtUtc = DateTime.UtcNow;
        return true;
    }

    public bool MarkCvDownloaded()
    {
        if (FirstCvDownloadedAtUtc is not null)
            return false;

        FirstCvDownloadedAtUtc = DateTime.UtcNow;
        return true;
    }

    private void EnsureActive(string action)
    {
        if (Status != ApplicationStatus.Active)
            throw new InvalidOperationException(
                $"An application in status '{Status}' cannot be {action}.");
    }
}
