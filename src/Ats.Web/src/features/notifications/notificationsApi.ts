import { apiClient } from '@/lib/apiClient';
import type { PagedResult } from '@/types/pagination';

/* `type` stays a string so an older bundle copes with new types. */
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
  /* Shape depends on `type`; read defensively in notificationText.ts. */
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

/* The candidate and company APIs differ only in base path; the recipient comes from the token. */
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
