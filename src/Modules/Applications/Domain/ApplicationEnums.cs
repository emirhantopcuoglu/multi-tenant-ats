namespace Ats.Modules.Applications.Domain;

// Lifecycle of a single application. Active is the only non-terminal state; the other
// three are final and cannot transition further.
public enum ApplicationStatus { Active, Withdrawn, Rejected, Hired }

// Classifies a pipeline stage so the system can reason about it without matching on the
// stage name (names are user-facing and may be customised later).
//   Initial       - where every new application starts
//   Active         - a normal working stage in the middle of the funnel
//   Interview      - the working stage where a scheduled interview belongs; a distinct type
//                    (rather than Active) so cross-module code — e.g. auto-advancing an
//                    application when an interview is scheduled — can find it without matching
//                    on the user-facing stage name
//   FinalHired     - terminal success
//   FinalRejected  - terminal rejection
public enum PipelineStageType { Initial, Active, Interview, FinalHired, FinalRejected }

// What happened to an application, recorded in the activity log. Each value pairs with a
// payload shape produced by the matching ApplicationActivity factory.
public enum ApplicationActivityType { Submitted, StageChanged, Rejected, Viewed }
