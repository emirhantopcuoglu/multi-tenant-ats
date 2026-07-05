import type { TFunction } from 'i18next';
import { INTERVIEW_TYPES, type InterviewType } from '@/types/enums';
import type { NotificationItem } from './notificationsApi';

export interface RenderedNotification {
  text: string;
  /** Deep-link target for the row click; null when the payload names no application. */
  applicationId: string | null;
}

/* Single place that turns a structured payload into localized text, so the bell dropdown and the
   notifications page can never drift apart. Payload fields are read defensively: the shape is a
   cross-module contract (NotificationPayloads.cs), not something this client controls. */
export function renderNotification(
  item: NotificationItem,
  t: TFunction,
  locale: string,
): RenderedNotification {
  switch (item.type) {
    case 'ApplicationStageChanged':
      return {
        text: t('notifications.stageChanged', {
          jobTitle: readString(item.payload, 'jobTitle'),
          stage: readString(item.payload, 'toStageName'),
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
    default:
      // A type this bundle predates: still show something clickable rather than a broken row.
      return { text: t('notifications.generic'), applicationId: null };
  }
}

/* "5 minutes ago" style timestamps for recent notifications; older ones switch to a plain date
   because "3 weeks ago" stops being useful. Intl covers both locales without a date library. */
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
