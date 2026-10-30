using Ats.Modules.Applications.Domain;

namespace Ats.Modules.Applications.Application.Applications;

// Which stage an application should move to when an interview is scheduled. Pure, so it can be
// tested without a database or bus.
public static class InterviewStageAdvancement
{
    // Null means do nothing: not Active, no Interview stage, or already at or past it.
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
