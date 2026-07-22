import { useMutation, useQuery, useQueryClient, type QueryKey } from '@tanstack/react-query';
import {
  candidateNotificationsApi,
  companyNotificationsApi,
  type NotificationsApi,
} from './notificationsApi';

/* Polling instead of a push channel: the project has no WebSocket/SSE infrastructure, and a badge
   that lags by up to 30s is fine. The backend exposes unread-count as a dedicated bare COUNT
   endpoint precisely so this poll stays cheap. */
const UNREAD_POLL_INTERVAL_MS = 30_000;

/* Same four hooks for both audiences, parameterized by the underlying API and a query-key prefix
   so the candidate and company caches never collide. `enabled` on useNotifications lets a bell
   dropdown defer fetching until it is actually opened. */
function createNotificationHooks(keyPrefix: QueryKey, api: NotificationsApi) {
  function useUnreadNotificationCount() {
    return useQuery({
      queryKey: [...keyPrefix, 'unread-count'],
      queryFn: api.getUnreadNotificationCount,
      refetchInterval: UNREAD_POLL_INTERVAL_MS,
    });
  }

  function useNotifications(page: number, pageSize: number, enabled = true) {
    return useQuery({
      queryKey: [...keyPrefix, 'list', page, pageSize],
      queryFn: () => api.listNotifications(page, pageSize),
      enabled,
      placeholderData: (prev) => prev,
    });
  }

  function useMarkNotificationRead() {
    const queryClient = useQueryClient();
    return useMutation({
      mutationFn: api.markNotificationRead,
      onSuccess: () => {
        void queryClient.invalidateQueries({ queryKey: keyPrefix });
      },
    });
  }

  function useMarkAllNotificationsRead() {
    const queryClient = useQueryClient();
    return useMutation({
      mutationFn: api.markAllNotificationsRead,
      onSuccess: () => {
        void queryClient.invalidateQueries({ queryKey: keyPrefix });
      },
    });
  }

  return { useUnreadNotificationCount, useNotifications, useMarkNotificationRead, useMarkAllNotificationsRead };
}

export const candidateNotifications = createNotificationHooks(
  ['candidate', 'notifications'],
  candidateNotificationsApi,
);
export const companyNotifications = createNotificationHooks(
  ['company', 'notifications'],
  companyNotificationsApi,
);
