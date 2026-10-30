namespace Ats.Modules.Applications.Domain;

// Active is the only non-terminal state.
public enum ApplicationStatus { Active, Withdrawn, Rejected, Hired }

// Lets code reason about stages without matching on their names.
//   Interview - where an application goes when an interview is scheduled
public enum PipelineStageType { Initial, Active, Interview, FinalHired, FinalRejected }

// Each value has a payload shape from the matching ApplicationActivity factory.
//   StageCorrected - a fix for a wrong move, in any direction, always with a reason
//   Withdrawn      - closed by the candidate; ActorUserId is null
public enum ApplicationActivityType
{
    Submitted, StageChanged, Rejected, Viewed, Hired, StageCorrected, Withdrawn
}
