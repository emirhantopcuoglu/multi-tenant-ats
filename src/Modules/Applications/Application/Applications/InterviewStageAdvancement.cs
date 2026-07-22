using Ats.Modules.Applications.Domain;

namespace Ats.Modules.Applications.Application.Applications;

// The decision behind AdvanceToInterviewStageConsumer (Infrastructure): given an application's
// current status/stage and its job's pipeline, which stage (if any) should it be moved to because
// an interview was just scheduled against it? Kept pure and separate from the consumer so the rule
// — forward-only, Active-only, and dependent on the pipeline having an Interview-type stage at all
// — is unit-testable without a database or a message bus.
public static class InterviewStageAdvancement
{
    // Null means "do nothing": either the application is no longer Active, its pipeline has no
    // stage of type Interview, or it is already at or past that stage (e.g. already in Offer) —
    // scheduling a follow-up interview must never pull an application backwards.
    public static PipelineStage? FindTarget(
        ApplicationStatus status, Guid currentStageId, IReadOnlyCollection<PipelineStage> stages)
    {
        if (status != ApplicationStatus.Active)
            return null;

        var interviewStage = stages.FirstOrDefault(s => s.Type == PipelineStageType.Interview && !s.IsDeleted);
        if (interviewStage is null)
            return null;

        var currentStage = stages.FirstOrDefault(s => s.Id == currentStageId);
        if (currentStage is not null && currentStage.Order >= interviewStage.Order)
            return null;

        return interviewStage;
    }
}
