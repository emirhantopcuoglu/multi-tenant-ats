import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Timeline, TimelineItem, type TimelineDotTone } from '@/components/ui';
import type { ApplicationActivity, PipelineStage } from '@/types/application';
import type { ApplicationActivityType } from '@/types/enums';

interface ActivityTimelineProps {
  activities: ApplicationActivity[];
  /** Used to resolve the stage ids in a StageChanged payload to readable names. */
  stages: PipelineStage[];
}

const toneByType: Record<ApplicationActivityType, TimelineDotTone> = {
  Submitted: 'accent',
  StageChanged: 'neutral',
  Rejected: 'danger',
};

/* Renders the append-only application history. The actor's display name isn't available yet (the
   activity carries only actorUserId and there's no user-lookup endpoint), so entries show the action
   and timestamp; resolving names can come once such an endpoint exists. */
export function ActivityTimeline({ activities, stages }: ActivityTimelineProps) {
  const { t, i18n } = useTranslation();
  const formatter = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' });

  const stageName = useMemo(() => {
    const byId = new Map(stages.map((stage) => [stage.id, stage.name]));
    return (id: unknown) => byId.get(String(id)) ?? '—';
  }, [stages]);

  return (
    <Timeline>
      {activities.map((activity, index) => {
        const last = index === activities.length - 1;
        const tone = toneByType[activity.activityType] ?? 'neutral';
        const meta = formatter.format(new Date(activity.occurredAtUtc));

        if (activity.activityType === 'StageChanged') {
          return (
            <TimelineItem
              key={activity.id}
              tone={tone}
              last={last}
              meta={meta}
              title={t('applicationDetail.activity.moved', {
                from: stageName(activity.payload.fromStageId),
                to: stageName(activity.payload.toStageId),
              })}
            />
          );
        }

        if (activity.activityType === 'Rejected') {
          return (
            <TimelineItem
              key={activity.id}
              tone={tone}
              last={last}
              meta={meta}
              title={t('applicationDetail.activity.rejected')}
            >
              {typeof activity.payload.reason === 'string' && (
                <p className="text-sm text-text-muted">{activity.payload.reason}</p>
              )}
            </TimelineItem>
          );
        }

        return (
          <TimelineItem
            key={activity.id}
            tone={tone}
            last={last}
            meta={meta}
            title={t('applicationDetail.activity.submitted')}
          />
        );
      })}
    </Timeline>
  );
}
