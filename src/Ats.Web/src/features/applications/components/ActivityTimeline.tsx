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
  /** Used to resolve the stage ids in a StageChanged payload to readable names. */
  stages: PipelineStage[];
}

/* Tones mirror the candidate tracking page (trackingSteps.ts) so both sides of the pipeline
   speak the same visual language. */
const displayByType: Record<ApplicationActivityType, { icon: IconTimelineIcon; tone: IconTimelineTone }> = {
  Submitted: { icon: 'submitted', tone: 'success' },
  Viewed: { icon: 'viewed', tone: 'accent' },
  StageChanged: { icon: 'movedTo', tone: 'accent' },
  Rejected: { icon: 'rejected', tone: 'danger' },
};

/* An activity type the frontend doesn't know yet must never masquerade as a known event (a
   missing 'Viewed' branch once rendered view receipts as "Application submitted"), so it falls
   back to the raw type name in a neutral tone. */
const unknownDisplay = { icon: 'upcoming' as IconTimelineIcon, tone: 'neutral' as IconTimelineTone };

/* Renders the append-only application history in story order (oldest first), matching the
   candidate tracking page. The actor's display name isn't available yet (the activity carries
   only actorUserId and there's no user-lookup endpoint), so entries show the action and
   timestamp. */
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
      case 'Rejected':
        return t('applicationDetail.activity.rejected');
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
            {activity.activityType === 'Rejected' && typeof activity.payload.reason === 'string' && (
              <p className="text-sm text-text-muted">{activity.payload.reason}</p>
            )}
          </IconTimelineItem>
        );
      })}
    </IconTimeline>
  );
}
