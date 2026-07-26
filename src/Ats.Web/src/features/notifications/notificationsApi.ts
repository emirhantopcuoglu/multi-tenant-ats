import { apiClient } from '@/lib/apiClient';
import type { PagedResult } from '@/types/pagination';

/* Server enum names from the Notifications module. `type` stays a plain string on the wire so a
   cached bundle keeps working when the API starts emitting types it doesn't know yet — rendering
   falls back to a generic message instead of crashing. */
export const KNOWN_NOTIFICATION_TYPES = [
  'ApplicationStageChanged',
  'InterviewScheduled',
  'ApplicationViewed',
  'ApplicationCvDownloaded',
  'NewApplication',
  'InterviewReminder',
] as const;

export interface NotificationItem {
  id: string;
  type: string;
  /* Structured payload (shape depends on `type`); the client renders localized text from it.
     Fields are read defensively in notificationText.ts — never trust the shape blindly. */
  payload: Record<string, unknown>;
  createdAtUtc: string;
  readAtUtc: string | null;
}

export interface NotificationsApi {
  listNotifications: (page?: number, pageSize?: number) => Promise<PagedResult<NotificationItem>>;
  getUnreadNotificationCount: () => Promise<number>;
  markNotificationRead: (id: string) => Promise<void>;
  markAllNotificationsRead: () => Promise<void>;
}

/* Candidates and company users each have their own notification feed, addressed from their own
   JWT (never a client-supplied id) — so the two APIs are identical apart from the base path. A
   factory avoids maintaining two copies of the same four calls. */
function createNotificationsApi(basePath: string): NotificationsApi {
  return {
    async listNotifications(page = 1, pageSize = 20) {
      const { data } = await apiClient.get<PagedResult<NotificationItem>>(basePath, {
        params: { page, pageSize },
      });
      return data;
    },
    async getUnreadNotificationCount() {
      const { data } = await apiClient.get<number>(`${basePath}/unread-count`);
      return data;
    },
    async markNotificationRead(id: string) {
      await apiClient.post(`${basePath}/${encodeURIComponent(id)}/read`);
    },
    async markAllNotificationsRead() {
      await apiClient.post(`${basePath}/read-all`);
    },
  };
}

export const candidateNotificationsApi = createNotificationsApi('/api/v1/candidate/notifications');
export const companyNotificationsApi = createNotificationsApi('/api/v1/notifications');
