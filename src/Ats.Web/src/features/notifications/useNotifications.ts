import { useMutation, useQuery, useQueryClient, type QueryKey } from '@tanstack/react-query';
import {
  candidateNotificationsApi,
  companyNotificationsApi,
  type NotificationsApi,
} from './notificationsApi';

/* Polling, since there is no WebSocket/SSE; up to 30s of lag is fine for a badge. */
const UNREAD_POLL_INTERVAL_MS = 30_000;

/* The key prefix keeps candidate and company caches apart. `enabled` lets the bell wait until opened. */
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
