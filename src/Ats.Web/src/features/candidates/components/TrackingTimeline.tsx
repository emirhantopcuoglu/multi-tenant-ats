import { useTranslation } from 'react-i18next';
import { stageLabel } from '@/lib/stageLabel';
import type { CandidateApplicationDetail } from '../candidateApplicationsApi';
import { isHiredStage, type TrackingStep, type TrackingTone } from '../trackingSteps';

/* Soft, tinted circle background + icon colour per step tone — the same semantic tones as Badge
   (lib/statusColors), reused here instead of the small solid dots the generic Timeline component
   uses, since this page wants a bigger, calmer visual. */
const toneClasses: Record<TrackingTone, string> = {
  accent: 'bg-accent-subtle text-accent',
  success: 'bg-success-bg text-success',
  danger: 'bg-danger-bg text-danger',
  warning: 'bg-warning-bg text-warning',
  neutral: 'bg-divider text-text-muted',
};

function iconPath(kind: TrackingStep['kind']): string {
  switch (kind) {
    case 'submitted':
      return 'M22 2 11 13M22 2l-7 20-4-9-9-4 20-7Z';
    case 'viewed':
      return 'M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7ZM12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z';
    case 'movedTo':
      return 'M5 12h14M13 6l6 6-6 6';
    case 'hired':
      return 'm5 13 4 4L19 7';
    case 'rejected':
      return 'M18 6 6 18M6 6l12 12';
    case 'current':
      return 'M12 8v4l3 3M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Z';
    case 'upcoming':
      return 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Z';
  }
}

function StepIcon({ kind }: { kind: TrackingStep['kind'] }) {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d={iconPath(kind)} />
    </svg>
  );
}

/* One step in the candidate's tracking view: a bigger tinted icon circle (instead of a small solid
   dot) with a connecting line, then the localized title, timestamp and stage name. Deliberately its
   own component rather than a reuse of the generic Timeline/TimelineItem — those stay small and are
   also used by the company's dense activity log, which shouldn't inherit this page's bigger, softer
   treatment. */
function TrackingTimelineStep({
  step,
  last,
  label,
  meta,
}: {
  step: TrackingStep;
  last: boolean;
  label: string;
  meta?: string;
}) {
  return (
    <li className="relative flex gap-4 pb-7 last:pb-0">
      {!last && <span aria-hidden="true" className="absolute left-[19px] top-10 bottom-0 w-px bg-border" />}
      <span
        className={`relative z-10 flex h-10 w-10 shrink-0 items-center justify-center rounded-full ${toneClasses[step.tone]}`}
      >
        <StepIcon kind={step.kind} />
      </span>
      <div className="space-y-0.5 pt-1.5">
        <p className={step.tone === 'neutral' ? 'text-sm text-text-muted' : 'text-sm font-medium text-text'}>
          {label}
        </p>
        {meta && <p className="text-xs text-text-muted">{meta}</p>}
      </div>
    </li>
  );
}

export function TrackingTimeline({
  steps,
  detail,
  dateFormatter,
}: {
  steps: TrackingStep[];
  detail: CandidateApplicationDetail;
  dateFormatter: Intl.DateTimeFormat;
}) {
  const { t } = useTranslation();

  return (
    <ol className="relative">
      {steps.map((step, index) => (
        <TrackingTimelineStep
          key={step.key}
          step={step}
          last={index === steps.length - 1}
          label={stepLabel(step, detail, t)}
          meta={
            step.occurredAtUtc
              ? dateFormatter.format(new Date(step.occurredAtUtc))
              : step.isCurrent
                ? t('candidatePortal.tracking.inReview')
                : undefined
          }
        />
      ))}
    </ol>
  );
}

/* One place decides the wording of every step. A stage move into the pipeline's FinalHired stage
   is celebrated rather than announced like an ordinary move. Stage names are translated here (at
   render time) rather than in trackingSteps.ts, which stays a pure data transform over the raw
   backend names. */
function stepLabel(
  step: TrackingStep,
  detail: CandidateApplicationDetail,
  t: ReturnType<typeof useTranslation>['t'],
): string {
  switch (step.kind) {
    case 'submitted':
      return t('candidatePortal.tracking.submitted');
    case 'viewed':
      return t('candidatePortal.tracking.viewed');
    case 'rejected':
      return t('candidatePortal.tracking.rejected');
    case 'hired':
      return step.occurredAtUtc === null
        ? (step.stageName ? stageLabel(step.stageName, t) : t('candidatePortal.tracking.hiredEvent'))
        : t('candidatePortal.tracking.hiredEvent');
    case 'movedTo':
      if (isHiredStage(step.stageName, detail.pipelineStages)) {
        return t('candidatePortal.tracking.hiredEvent');
      }
      return step.stageName
        ? t('candidatePortal.tracking.movedTo', { stage: stageLabel(step.stageName, t) })
        : t('candidatePortal.tracking.moved');
    case 'current':
    case 'upcoming':
      return step.stageName ? stageLabel(step.stageName, t) : '';
  }
}
