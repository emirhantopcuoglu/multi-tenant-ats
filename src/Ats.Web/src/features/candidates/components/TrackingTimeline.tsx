import type { TFunction } from 'i18next';
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
          /* Dated for everything that has happened, including the current stage; blank for the
             steps still ahead. */
          meta={
            step.occurredAtUtc ? dateFormatter.format(new Date(step.occurredAtUtc)) : undefined
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
  t: TFunction,
): string {
  switch (step.label) {
    case 'submitted':
      return t('candidatePortal.tracking.submitted');
    case 'viewed':
      return t('candidatePortal.tracking.viewed');
    case 'rejected':
      return t('candidatePortal.tracking.rejected');
    case 'hired':
      return t('candidatePortal.tracking.hiredEvent');
    case 'movedTo':
      if (isHiredStage(step.stageName, detail.pipelineStages)) {
        return t('candidatePortal.tracking.hiredEvent');
      }
      return step.stageName
        ? t('candidatePortal.tracking.movedTo', { stage: stageLabel(step.stageName, t) })
        : t('candidatePortal.tracking.moved');
    // Reads as a state ("you are in X"), not an event, and carries the date it was entered.
    case 'current':
      return step.stageName
        ? t('candidatePortal.tracking.currentStage', { stage: stageLabel(step.stageName, t) })
        : '';
    case 'upcoming':
      return step.stageName ? stageLabel(step.stageName, t) : '';
  }
}
