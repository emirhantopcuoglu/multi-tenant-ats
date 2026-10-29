using Ats.Shared.Kernel;

namespace Ats.Modules.Applications.Domain;

// Every job gets the same default stages. The pipeline owns its stages.
public sealed class Pipeline : ITenantScoped, IAuditable, ISoftDeletable
{
    // Names are shown to users; behaviour depends on Type.
    private const string AppliedStage = "Applied";
    private const string ScreeningStage = "Screening";
    private const string InterviewStage = "Interview";
    private const string OfferStage = "Offer";
    private const string HiredStage = "Hired";
    private const string RejectedStage = "Rejected";

    private readonly List<PipelineStage> _stages = [];

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid JobId { get; private set; }
    public IReadOnlyCollection<PipelineStage> Stages => _stages.AsReadOnly();

    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public Guid? DeletedBy { get; private set; }

    private Pipeline() { }

    private Pipeline(Guid id, Guid jobId)
    {
        Id = id;
        JobId = jobId;
    }

    public static Pipeline CreateDefault(Guid jobId)
    {
        if (jobId == Guid.Empty)
            throw new ArgumentException("JobId is required.", nameof(jobId));

        var pipeline = new Pipeline(Guid.NewGuid(), jobId);
        pipeline.AddStage(AppliedStage, order: 1, PipelineStageType.Initial);
        pipeline.AddStage(ScreeningStage, order: 2, PipelineStageType.Active);
        pipeline.AddStage(InterviewStage, order: 3, PipelineStageType.Interview);
        pipeline.AddStage(OfferStage, order: 4, PipelineStageType.Active);
        pipeline.AddStage(HiredStage, order: 5, PipelineStageType.FinalHired);
        pipeline.AddStage(RejectedStage, order: 6, PipelineStageType.FinalRejected);
        return pipeline;
    }

    // Single, not FirstOrDefault: anything but exactly one Initial stage is a bug.
    public PipelineStage InitialStage => _stages.Single(s => s.Type == PipelineStageType.Initial);

    private void AddStage(string name, int order, PipelineStageType type)
        => _stages.Add(new PipelineStage(Id, name, order, type));
}
