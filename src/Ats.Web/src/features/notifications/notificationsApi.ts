import { apiClient } from '@/lib/apiClient';
import type { PagedResult } from '@/types/pagination';

/* Server enum names from the Notifications module. `type` stays a plain string on the wire so a
   cached bundle keeps working when the API starts emitting types it doesn't know yet — rendering
   falls back to a generic message instead of crashing. */
export const KNOWN_NOTIFICATION_TYPES = ['ApplicationStageChanged', 'InterviewScheduled'] as const;

export interface NotificationItem {
  id: string;
  type: string;
  /* Structured payload (shape depends on `type`); the client renders localized text from it.
     Fields are read defensively in notificationText.ts — never trust the shape blindly. */
  payload: Record<string, unknown>;
  createdAtUtc: string;
  readAtUtc: string | null;
}

const NOTIFICATIONS_URL = '/api/v1/candidate/notifications';

export async function listNotifications(
  page = 1,
  pageSize = 20,
): Promise<PagedResult<NotificationItem>> {
  const { data } = await apiClient.get<PagedResult<NotificationItem>>(NOTIFICATIONS_URL, {
    params: { page, pageSize },
  });
  return data;
}

export async function getUnreadNotificationCount(): Promise<number> {
  const { data } = await apiClient.get<number>(`${NOTIFICATIONS_URL}/unread-count`);
  return data;
}

export async function markNotificationRead(id: string): Promise<void> {
  await apiClient.post(`${NOTIFICATIONS_URL}/${encodeURIComponent(id)}/read`);
}

export async function markAllNotificationsRead(): Promise<void> {
  await apiClient.post(`${NOTIFICATIONS_URL}/read-all`);
}
