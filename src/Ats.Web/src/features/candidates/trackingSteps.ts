import type { IconTimelineIcon, IconTimelineTone } from '@/components/ui';
import type {
  CandidateApplicationDetail,
  CandidatePipelineStage,
  CandidateTimelineEntry,
} from './candidateApplicationsApi';

/* Turns the backend's raw material (timeline events + full pipeline) into the single vertical
   flow the tracking page renders:

     what happened (dated, coloured)  →  where the application is now  →  what comes next (grey)

   Pure on purpose: the page stays a dumb renderer and this logic is unit-testable without React. */

export type TrackingTone = IconTimelineTone;

/* What the step says, kept apart from `kind` (what it looks like). Stage steps now pick an icon
   from the stage itself, so the two can no longer be the same field. */
export type TrackingLabel =
  | 'submitted'
  | 'viewed'
  | 'rejected'
  | 'hired'
  | 'withdrawn'
  | 'movedTo'
  | 'current'
  | 'upcoming';

export interface TrackingStep {
  key: string;
  kind: IconTimelineIcon;
  label: TrackingLabel;
  stageName: string | null;
  occurredAtUtc: string | null;
  tone: TrackingTone;
  isCurrent: boolean;
}

export function buildTrackingSteps(detail: CandidateApplicationDetail): TrackingStep[] {
  // The event that put the application in its current stage is not listed separately: it becomes
  // the current step, dated. Otherwise the same moment reads twice — "moved to X" followed by
  // "X, in review" with no date.
  const currentEntryIndex = detail.status === 'Active' ? findCurrentStageEntry(detail) : -1;

  const steps = detail.timeline
    .map(toEventStep)
    .filter((_, index) => index !== currentEntryIndex);

  // The forward-looking part only exists while the application is in play: a terminal application's
  // story is fully told by its events. Showing the stages still ahead of a candidate who withdrew
  // would read as a process they are still in.
  if (detail.status === 'Active') {
    const enteredAtUtc =
      currentEntryIndex >= 0 ? detail.timeline[currentEntryIndex].occurredAtUtc : detail.appliedAtUtc;
    steps.push(...buildRoadmap(detail, enteredAtUtc));
  }

  return steps;
}

/* Index of the StageChanged event that moved the application into the stage it is in now, or -1.
   Searched from the end: a stage can be entered more than once (a correction, a move back), and
   only the latest arrival is when the candidate actually got here. */
function findCurrentStageEntry(detail: CandidateApplicationDetail): number {
  const current = detail.pipelineStages.find((s) => s.id === detail.currentStageId);
  if (!current) return -1;

  for (let index = detail.timeline.length - 1; index >= 0; index -= 1) {
    const entry = detail.timeline[index];
    if (entry.type === 'StageChanged' && entry.stageName === current.name) return index;
  }
  return -1;
}

function toEventStep(entry: CandidateTimelineEntry, index: number): TrackingStep {
  const base = {
    key: `event-${index}`,
    stageName: entry.stageName,
    occurredAtUtc: entry.occurredAtUtc,
    isCurrent: false,
  };

  switch (entry.type) {
    case 'Submitted':
      return { ...base, kind: 'submitted', label: 'submitted', tone: 'success' };
    case 'Viewed':
      return { ...base, kind: 'viewed', label: 'viewed', tone: 'accent' };
    case 'Rejected':
      return { ...base, kind: 'rejected', label: 'rejected', tone: 'danger' };
    case 'Hired':
      return { ...base, kind: 'hired', label: 'hired', tone: 'success' };
    // Neutral rather than danger: the candidate chose to stop, which is not a setback to report
    // back to them in the same colour as a rejection.
    case 'Withdrawn':
      return { ...base, kind: 'withdrawn', label: 'withdrawn', tone: 'neutral' };
    case 'StageChanged':
      return { ...base, kind: 'movedTo', label: 'movedTo', tone: 'accent' };
  }
}

/* The current stage, dated with when it was entered, then the stages still ahead in funnel order.
   FinalRejected is never on the roadmap — it is an exit, not a step, and showing it ahead of an
   active candidate would read as a threat. */
function buildRoadmap(
  detail: CandidateApplicationDetail,
  enteredAtUtc: string | null,
): TrackingStep[] {
  const current = detail.pipelineStages.find((s) => s.id === detail.currentStageId);
  if (!current) return [];

  const ahead = detail.pipelineStages.filter(
    (stage) => stage.order > current.order && stage.type !== 'FinalRejected',
  );

  return [
    {
      key: `current-${current.id}`,
      kind: stageIcon(current),
      label: 'current',
      stageName: current.name,
      occurredAtUtc: enteredAtUtc,
      tone: 'warning',
      isCurrent: true,
    },
    ...ahead.map(
      (stage): TrackingStep => ({
        key: `upcoming-${stage.id}`,
        kind: stageIcon(stage),
        label: 'upcoming',
        stageName: stage.name,
        occurredAtUtc: null,
        tone: 'neutral',
        isCurrent: false,
      }),
    ),
  ];
}

/* A recognisable icon per stage, so the roadmap is not a column of identical circles. Keyed on the
   stage's type first (the only thing the backend guarantees), falling back to the default pipeline's
   stage names — a tenant that renamed or added stages still gets the generic type icon. */
function stageIcon(stage: CandidatePipelineStage): IconTimelineIcon {
  switch (stage.type) {
    case 'FinalHired':
      return 'hired';
    case 'FinalRejected':
      return 'rejected';
    case 'Interview':
      return 'interview';
    case 'Initial':
      return 'submitted';
    case 'Active':
      return activeStageIcon(stage.name);
  }
}

/* Both screening and offer are PipelineStageType.Active, so the type alone cannot tell them apart.
   Matching the default pipeline's names is a display nicety, not a rule: anything unrecognised
   falls back to the neutral stage icon rather than guessing. */
function activeStageIcon(name: string): IconTimelineIcon {
  const normalized = name.trim().toLowerCase();
  if (normalized === 'screening') return 'screening';
  if (normalized === 'offer') return 'offer';
  return 'stage';
}

/* True when the moved-to stage is the pipeline's FinalHired stage, so the page can celebrate
   the event instead of announcing it like any other move. */
export function isHiredStage(
  stageName: string | null,
  pipelineStages: CandidatePipelineStage[],
): boolean {
  return (
    stageName !== null &&
    pipelineStages.some((s) => s.name === stageName && s.type === 'FinalHired')
  );
}
