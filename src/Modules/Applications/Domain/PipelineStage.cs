using Ats.Shared.Kernel;

namespace Ats.Modules.Applications.Domain;

// Only created by its Pipeline, hence the internal constructor.
public sealed class PipelineStage : ITenantScoped, IAuditable, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PipelineId { get; private set; }
    public string Name { get; private set; } = null!;
    public int Order { get; private set; }
    public PipelineStageType Type { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public Guid? DeletedBy { get; private set; }

    private PipelineStage() { }

    internal PipelineStage(Guid pipelineId, string name, int order, PipelineStageType type)
    {
        Id = Guid.NewGuid();
        PipelineId = pipelineId;
        Name = name;
        Order = order;
        Type = type;
    }
}
