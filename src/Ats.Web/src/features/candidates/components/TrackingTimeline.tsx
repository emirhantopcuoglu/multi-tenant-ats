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
          /* Blank for steps still ahead. */
          meta={
            step.occurredAtUtc ? dateFormatter.format(new Date(step.occurredAtUtc)) : undefined
          }
        />
      ))}
    </IconTimeline>
  );
}

/* All step wording lives here. Stage names are translated here, keeping trackingSteps.ts pure. */
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
    case 'withdrawn':
      return t('candidatePortal.tracking.withdrawnEvent');
    case 'movedTo':
      if (isHiredStage(step.stageName, detail.pipelineStages)) {
        return t('candidatePortal.tracking.hiredEvent');
      }
      return step.stageName
        ? t('candidatePortal.tracking.movedTo', { stage: stageLabel(step.stageName, t) })
        : t('candidatePortal.tracking.moved');
    case 'current':
      return step.stageName
        ? t('candidatePortal.tracking.currentStage', { stage: stageLabel(step.stageName, t) })
        : '';
    case 'upcoming':
      return step.stageName ? stageLabel(step.stageName, t) : '';
  }
}
