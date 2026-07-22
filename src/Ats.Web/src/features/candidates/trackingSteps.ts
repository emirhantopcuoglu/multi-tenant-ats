import type { IconTimelineIcon, IconTimelineTone } from '@/components/ui';
import type {
  CandidateApplicationDetail,
  CandidatePipelineStage,
  CandidateTimelineEntry,
} from './candidateApplicationsApi';

/* Turns the backend's raw material (timeline events + full pipeline) into the single vertical
   flow the tracking page renders:

     what happened (dated, coloured)  →  where the application is now  →  what comes next (grey)

   Pure on purpose: the page stays a dumb renderer and this logic is unit-testable without React.
   Tones and kinds map onto the IconTimeline component's palette and icon set. */

export type TrackingTone = IconTimelineTone;

export interface TrackingStep {
  key: string;
  /* Which i18n label to render. 'stage' steps show the raw stage name instead. */
  kind: IconTimelineIcon;
  stageName: string | null;
  occurredAtUtc: string | null;
  tone: TrackingTone;
  isCurrent: boolean;
}

export function buildTrackingSteps(detail: CandidateApplicationDetail): TrackingStep[] {
  const steps = detail.timeline.map(toEventStep);

  // The forward-looking part only exists while the application is in play: a terminal
  // application's story is fully told by its events (plus the status badge for Withdrawn,
  // which never produces a timeline event).
  if (detail.status === 'Active') {
    steps.push(...buildRoadmap(detail));
  }

  return steps;
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
      return { ...base, kind: 'submitted', tone: 'success' };
    case 'Viewed':
      return { ...base, kind: 'viewed', tone: 'accent' };
    case 'Rejected':
      return { ...base, kind: 'rejected', tone: 'danger' };
    case 'Hired':
      return { ...base, kind: 'hired', tone: 'success' };
    case 'StageChanged':
      return { ...base, kind: 'movedTo', tone: 'accent' };
  }
}

/* The current stage (highlighted, "in review") followed by the stages still ahead of it in
   funnel order. The FinalRejected stage is never part of the roadmap — it is an exit, not a
   step on the path, and showing it ahead of an active candidate would read as a threat. */
function buildRoadmap(detail: CandidateApplicationDetail): TrackingStep[] {
  const current = detail.pipelineStages.find((s) => s.id === detail.currentStageId);
  if (!current) return [];

  const ahead = detail.pipelineStages.filter(
    (stage) => stage.order > current.order && stage.type !== 'FinalRejected',
  );

  return [
    {
      key: `current-${current.id}`,
      kind: 'current',
      stageName: current.name,
      occurredAtUtc: null,
      tone: 'warning',
      isCurrent: true,
    },
    ...ahead.map(
      (stage): TrackingStep => ({
        key: `upcoming-${stage.id}`,
        kind: stage.type === 'FinalHired' ? 'hired' : 'upcoming',
        stageName: stage.name,
        occurredAtUtc: null,
        tone: 'neutral',
        isCurrent: false,
      }),
    ),
  ];
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
