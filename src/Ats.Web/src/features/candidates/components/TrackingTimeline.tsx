import { useTranslation } from 'react-i18next';
import { IconTimeline, IconTimelineItem } from '@/components/ui';
import { stageLabel } from '@/lib/stageLabel';
import type { CandidateApplicationDetail } from '../candidateApplicationsApi';
import { isHiredStage, type TrackingStep } from '../trackingSteps';

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
    <IconTimeline>
      {steps.map((step, index) => (
        <IconTimelineItem
          key={step.key}
          icon={step.kind}
          tone={step.tone}
          last={index === steps.length - 1}
          title={stepLabel(step, detail, t)}
          meta={
            step.occurredAtUtc
              ? dateFormatter.format(new Date(step.occurredAtUtc))
              : step.isCurrent
                ? t('candidatePortal.tracking.inReview')
                : undefined
          }
        />
      ))}
    </IconTimeline>
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
