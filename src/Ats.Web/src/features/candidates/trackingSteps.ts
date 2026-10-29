import type { IconTimelineIcon, IconTimelineTone } from '@/components/ui';
import type {
  CandidateApplicationDetail,
  CandidatePipelineStage,
  CandidateTimelineEntry,
} from './candidateApplicationsApi';

/* Turns the timeline and pipeline into one list: what happened, where it is now, what comes next.
   Pure, so it can be tested without React. */

export type TrackingTone = IconTimelineTone;

/* Kept separate from `kind`: stage steps choose their icon from the stage. */
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
  // The move into the current stage becomes the current step, so it isn't listed twice.
  const currentEntryIndex = detail.status === 'Active' ? findCurrentStageEntry(detail) : -1;

  const steps = detail.timeline
    .map(toEventStep)
    .filter((_, index) => index !== currentEntryIndex);

  // Upcoming stages only while the application is active.
  if (detail.status === 'Active') {
    const enteredAtUtc =
      currentEntryIndex >= 0 ? detail.timeline[currentEntryIndex].occurredAtUtc : detail.appliedAtUtc;
    steps.push(...buildRoadmap(detail, enteredAtUtc));
  }

  return steps;
}

/* Searched from the end: a stage can be entered more than once. */
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
    // Neutral, not danger: the candidate chose to stop.
    case 'Withdrawn':
      return { ...base, kind: 'withdrawn', label: 'withdrawn', tone: 'neutral' };
    case 'StageChanged':
      return { ...base, kind: 'movedTo', label: 'movedTo', tone: 'accent' };
  }
}

/* FinalRejected is never shown as an upcoming step. */
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

/* By stage type first, then the default pipeline's names; anything else gets the generic icon. */
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

/* Screening and offer are both Active, so the name is the only way to tell them apart. */
function activeStageIcon(name: string): IconTimelineIcon {
  const normalized = name.trim().toLowerCase();
  if (normalized === 'screening') return 'screening';
  if (normalized === 'offer') return 'offer';
  return 'stage';
}

export function isHiredStage(
  stageName: string | null,
  pipelineStages: CandidatePipelineStage[],
): boolean {
  return (
    stageName !== null &&
    pipelineStages.some((s) => s.name === stageName && s.type === 'FinalHired')
  );
}
