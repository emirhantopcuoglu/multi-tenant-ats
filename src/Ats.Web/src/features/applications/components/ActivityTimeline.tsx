import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import {
  IconTimeline,
  IconTimelineItem,
  type IconTimelineIcon,
  type IconTimelineTone,
} from '@/components/ui';
import { stageLabel } from '@/lib/stageLabel';
import type { ApplicationActivity, PipelineStage } from '@/types/application';
import type { ApplicationActivityType } from '@/types/enums';

interface ActivityTimelineProps {
  activities: ApplicationActivity[];
  /* Resolves stage ids in StageChanged payloads to names. */
  stages: PipelineStage[];
}

/* Same tones as the candidate tracking page (trackingSteps.ts). */
const displayByType: Record<ApplicationActivityType, { icon: IconTimelineIcon; tone: IconTimelineTone }> = {
  Submitted: { icon: 'submitted', tone: 'success' },
  Viewed: { icon: 'viewed', tone: 'accent' },
  StageChanged: { icon: 'movedTo', tone: 'accent' },
  // Warning tone: a correction is a fix, not progress.
  StageCorrected: { icon: 'movedTo', tone: 'warning' },
  Rejected: { icon: 'rejected', tone: 'danger' },
  Hired: { icon: 'hired', tone: 'success' },
  // Neutral: the candidate left; nobody did anything wrong.
  Withdrawn: { icon: 'withdrawn', tone: 'neutral' },
};

/* Unknown types show the raw name instead of passing for a known event. */
const unknownDisplay = { icon: 'upcoming' as IconTimelineIcon, tone: 'neutral' as IconTimelineTone };

/* Oldest first, like the candidate page. No actor names: there's no user-lookup endpoint. */
export function ActivityTimeline({ activities, stages }: ActivityTimelineProps) {
  const { t, i18n } = useTranslation();
  const formatter = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' });

  const ordered = useMemo(
    () =>
      [...activities].sort(
        (a, b) => new Date(a.occurredAtUtc).getTime() - new Date(b.occurredAtUtc).getTime(),
      ),
    [activities],
  );

  const stageName = useMemo(() => {
    const byId = new Map(stages.map((stage) => [stage.id, stage.name]));
    return (id: unknown) => {
      const rawName = byId.get(String(id));
      return rawName ? stageLabel(rawName, t) : '—';
    };
  }, [stages, t]);

  const titleOf = (activity: ApplicationActivity): string => {
    switch (activity.activityType) {
      case 'Submitted':
        return t('applicationDetail.activity.submitted');
      case 'Viewed':
        return t('applicationDetail.activity.viewed');
      case 'StageChanged':
        return t('applicationDetail.activity.moved', {
          from: stageName(activity.payload.fromStageId),
          to: stageName(activity.payload.toStageId),
        });
      case 'StageCorrected':
        return t('applicationDetail.activity.corrected', {
          from: stageName(activity.payload.fromStageId),
          to: stageName(activity.payload.toStageId),
        });
      case 'Rejected':
        return t('applicationDetail.activity.rejected');
      case 'Hired':
        return t('applicationDetail.activity.hired');
      case 'Withdrawn':
        return t('applicationDetail.activity.withdrawn');
      default:
        return activity.activityType;
    }
  };

  return (
    <IconTimeline>
      {ordered.map((activity, index) => {
        const { icon, tone } = displayByType[activity.activityType] ?? unknownDisplay;

        return (
          <IconTimelineItem
            key={activity.id}
            icon={icon}
            tone={tone}
            last={index === ordered.length - 1}
            title={titleOf(activity)}
            meta={formatter.format(new Date(activity.occurredAtUtc))}
          >
            {(activity.activityType === 'Rejected' || activity.activityType === 'StageCorrected') &&
              typeof activity.payload.reason === 'string' && (
                <p className="text-sm text-text-muted">{activity.payload.reason}</p>
              )}
          </IconTimelineItem>
        );
      })}
    </IconTimeline>
  );
}
