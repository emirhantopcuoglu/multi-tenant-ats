import type { TFunction } from 'i18next';
import { stageLabel } from '@/lib/stageLabel';
import { INTERVIEW_TYPES, type InterviewType } from '@/types/enums';
import type { NotificationItem } from './notificationsApi';

export interface RenderedNotification {
  text: string;
  /* Null when the payload has no application. */
  applicationId: string | null;
}

/* The one place payloads become text. Fields are read defensively: the shape is set by
   NotificationPayloads.cs. */
export function renderNotification(
  item: NotificationItem,
  t: TFunction,
  locale: string,
): RenderedNotification {
  switch (item.type) {
    case 'NewApplication':
      return {
        text: t('notifications.newApplication', {
          jobTitle: readString(item.payload, 'jobTitle'),
          candidateName: [
            readString(item.payload, 'candidateFirstName'),
            readString(item.payload, 'candidateLastName'),
          ]
            .filter(Boolean)
            .join(' '),
        }),
        applicationId: readString(item.payload, 'applicationId') || null,
      };
    case 'ApplicationViewed':
      return {
        text: t('notifications.applicationViewed', {
          jobTitle: readString(item.payload, 'jobTitle'),
        }),
        applicationId: readString(item.payload, 'applicationId') || null,
      };
    case 'ApplicationCvDownloaded':
      return {
        text: t('notifications.cvDownloaded', {
          jobTitle: readString(item.payload, 'jobTitle'),
        }),
        applicationId: readString(item.payload, 'applicationId') || null,
      };
    case 'ApplicationStageChanged':
      return {
        // stageLabel translates the default stage names.
        text: t('notifications.stageChanged', {
          jobTitle: readString(item.payload, 'jobTitle'),
          stage: stageLabel(readString(item.payload, 'toStageName'), t),
        }),
        applicationId: readString(item.payload, 'applicationId') || null,
      };
    case 'InterviewScheduled':
      return {
        text: t('notifications.interviewScheduled', {
          jobTitle: readString(item.payload, 'jobTitle'),
          type: interviewTypeLabel(readString(item.payload, 'interviewType'), t),
          date: formatDateTime(readString(item.payload, 'scheduledAtUtc'), locale),
        }),
        applicationId: readString(item.payload, 'applicationId') || null,
      };
    case 'InterviewRescheduled':
      return {
        // Both times, so the change is visible.
        text: t('notifications.interviewRescheduled', {
          jobTitle: readString(item.payload, 'jobTitle'),
          type: interviewTypeLabel(readString(item.payload, 'interviewType'), t),
          previousDate: formatDateTime(readString(item.payload, 'previousScheduledAtUtc'), locale),
          date: formatDateTime(readString(item.payload, 'scheduledAtUtc'), locale),
        }),
        applicationId: readString(item.payload, 'applicationId') || null,
      };
    case 'InterviewCancelled':
      return {
        text: t('notifications.interviewCancelled', {
          jobTitle: readString(item.payload, 'jobTitle'),
          type: interviewTypeLabel(readString(item.payload, 'interviewType'), t),
          date: formatDateTime(readString(item.payload, 'scheduledAtUtc'), locale),
        }),
        applicationId: readString(item.payload, 'applicationId') || null,
      };
    case 'InterviewReminder':
      return {
        // Unknown kinds fall back to the day-before wording, which states the date.
        text: t(
          readString(item.payload, 'reminderKind') === 'StartingSoon'
            ? 'notifications.interviewReminderStartingSoon'
            : 'notifications.interviewReminderDayBefore',
          {
            jobTitle: readString(item.payload, 'jobTitle'),
            type: interviewTypeLabel(readString(item.payload, 'interviewType'), t),
            date: formatDateTime(readString(item.payload, 'scheduledAtUtc'), locale),
          },
        ),
        applicationId: readString(item.payload, 'applicationId') || null,
      };
    default:
      // Unknown type: still show a clickable row.
      return { text: t('notifications.generic'), applicationId: null };
  }
}

/* Relative time for recent items, a plain date after that. */
export function formatNotificationTime(isoUtc: string, locale: string): string {
  const MS_PER_MINUTE = 60_000;
  const MINUTES_PER_HOUR = 60;
  const HOURS_PER_DAY = 24;
  const RELATIVE_CUTOFF_DAYS = 7;

  const date = new Date(isoUtc);
  const relative = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' });
  const elapsedMinutes = Math.round((Date.now() - date.getTime()) / MS_PER_MINUTE);

  if (elapsedMinutes < MINUTES_PER_HOUR) {
    return relative.format(-Math.max(elapsedMinutes, 0), 'minute');
  }

  const elapsedHours = Math.round(elapsedMinutes / MINUTES_PER_HOUR);
  if (elapsedHours < HOURS_PER_DAY) {
    return relative.format(-elapsedHours, 'hour');
  }

  const elapsedDays = Math.round(elapsedHours / HOURS_PER_DAY);
  if (elapsedDays < RELATIVE_CUTOFF_DAYS) {
    return relative.format(-elapsedDays, 'day');
  }

  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium' }).format(date);
}

function readString(payload: Record<string, unknown>, key: string): string {
  const value = payload[key];
  return typeof value === 'string' ? value : '';
}

function interviewTypeLabel(raw: string, t: TFunction): string {
  return (INTERVIEW_TYPES as readonly string[]).includes(raw)
    ? t(`interviewType.${raw as InterviewType}`)
    : raw;
}

function formatDateTime(isoUtc: string, locale: string): string {
  const date = new Date(isoUtc);
  if (Number.isNaN(date.getTime())) {
    return '';
  }
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(date);
}
